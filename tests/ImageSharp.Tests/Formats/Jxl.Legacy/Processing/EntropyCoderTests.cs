// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Jxl.IO.Entropy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Ans;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Modular.Encoding.ContextPrediction;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Tests.Memory;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing;

public class EntropyCoderTests
{
    /// <summary>
    /// Checks an actual learned split with dirty allocator storage and confirms that search and sample buffers are returned.
    /// </summary>
    [Fact]
    public void TreeSearch_SplitsDistinctChannelsAndReturnsOwnedStorage()
    {
        static void RunTest()
        {
            Configuration configuration = TestEnvironment.Configuration.Clone();
            TestMemoryAllocator allocator = new();
            allocator.EnableNonThreadSafeLogging();
            configuration.MemoryAllocator = allocator;
            JxlTreeSamples samples = new(configuration);

            try
            {
                samples.SetPredictor(JxlPredictor.Zero, JxlTreeMode.Default);
                samples.SetProperties([0, 1], JxlTreeMode.Default);
                samples.PrepareForSamples(4);
                InlineArray2<InlineArray2<int>> range = default;
                range[0][1] = 2;
                range[1][1] = 1;
                samples.PreQuantizeProperties(range, [], [4], [2, 2], [], [], 2);
                Span<int> properties = [0, 0];
                Span<int> predictions = stackalloc int[JxlPredictorFacts.ModularPredictors];
                predictions.Clear();
                samples.AddSample(0, properties, predictions);
                samples.AddSample(0, properties, predictions);
                properties[0] = 1;
                samples.AddSample(16, properties, predictions);
                samples.AddSample(16, properties, predictions);
                samples.AllSamplesDone();
                long sampleBytes = allocator.LiveBufferBytes;

                // Each channel has one repeated token. Splitting at channel 0 removes four entropy bits, with payload bits unchanged.
                // Residual 16 encodes to token 24, so the real search also traverses complete 512-bit histogram batches.
                List<JxlPropertyDecisionNode> tree = [];
                JxlMaEncoder.ComputeBestTree(configuration, ref samples, 0.5F, [], range, 1F, tree);
                Assert.Equal(3, tree.Count);
                Assert.Equal(0, tree[0].Property);
                Assert.Equal(0, tree[0].SplitValue);
                Assert.Equal(-1, tree[tree[0].LeftChild].Property);
                Assert.Equal(-1, tree[tree[0].RightChild].Property);
                Assert.Equal(JxlPredictor.Zero, tree[tree[0].LeftChild].Predictor);
                Assert.Equal(JxlPredictor.Zero, tree[tree[0].RightChild].Predictor);
                Assert.Equal(sampleBytes, allocator.LiveBufferBytes);
            }
            finally
            {
                samples.Dispose();
            }

            Assert.Equal(0L, allocator.LiveBufferBytes);
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Checks ANS cost for exact binary probabilities, zero bins and logical histogram lengths under every feature mode.
    /// </summary>
    [Fact]
    public void HistogramBitCost_UsesProbabilityFloorAndLogicalCount()
    {
        static void RunTest()
        {
            ReadOnlySpan<int> widths = [1, 2, 4, 8, 16, 32, 64];
            foreach (int width in widths)
            {
                int[] counts = new int[width + 1];
                counts.AsSpan(0, width).Fill(4);
                counts[width] = int.MaxValue;

                // Equal frequencies require log2(width) bits per occurrence. Powers of two make this expectation exact.
                float expected = 4 * width * (int)BitOperations.Log2((uint)width);
                Assert.Equal(expected, JxlMaEncoder.EstimateBits(counts, width));

                counts.AsSpan(0, width).Clear();
                Assert.Equal(0F, JxlMaEncoder.EstimateBits(counts, width));
                counts[width - 1] = 7;
                Assert.Equal(0F, JxlMaEncoder.EstimateBits(counts, width));
                Assert.Equal(int.MaxValue, counts[width]);
            }

            ReadOnlySpan<int> lengths = [3, 7, 15, 17, 31, 33, 65];
            foreach (int length in lengths)
            {
                int[] counts = new int[length + 1];
                counts[0] = 1;
                counts[length - 1] = 1;
                counts[length] = int.MaxValue;

                // Two equally likely symbols require one bit each. Empty bins must contribute zero, including scalar tail bins.
                Assert.Equal(2F, JxlMaEncoder.EstimateBits(counts, length));
            }

            // ANS has 4096 table entries, so the probability floor limits this estimate to 12 bits per occurrence.
            // With 8192 equally likely symbols, unconstrained Shannon entropy would incorrectly return 13 bits each.
            int[] rareSymbols = new int[8192];
            rareSymbols.AsSpan().Fill(1);
            Assert.Equal(98304F, JxlMaEncoder.EstimateBits(rareSymbols, rareSymbols.Length));
            Assert.Equal(0F, JxlMaEncoder.EstimateBits([], 0));
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Checks sample insertion, independent predictor planes, static and dynamic maps, duplicate counts, and in-place partition swaps.
    /// </summary>
    [Fact]
    public void TreeSamples_RetainNewRecordsAndMergeOnlyEqualQuantizedSamples()
    {
        JxlTreeSamples samples = new(TestEnvironment.Configuration);
        try
        {
            samples.SetPredictor(JxlPredictor.Best, JxlTreeMode.Default);
            Span<int> selected = [0, 1, 6];
            samples.SetProperties(selected, JxlTreeMode.Default);
            selected[2] = JxlMaConstants.WpProp;
            samples.PrepareForSamples(5);

            // Two equal channel counts split at channel 0. Group counts [0,0,1,1] instead split at group 2.
            // Pixel training values [-8,8] split at -8. Each exact threshold stays in the lower interval.
            InlineArray2<InlineArray2<int>> range = default;
            range[0][1] = 2;
            range[1][1] = 4;
            Span<int> pixels = [-8, 8];
            samples.PreQuantizeProperties(range, [], [0, 0, 1, 1], [1, 1], pixels, [], 2);
            Assert.Equal(6, samples.PropertyFromIndex(2));
            Assert.Equal(0, samples.QuantizeStaticProperty(0, 0));
            Assert.Equal(1, samples.QuantizeStaticProperty(0, 1));
            Assert.Equal(0, samples.QuantizeStaticProperty(1, 2));
            Assert.Equal(1, samples.QuantizeStaticProperty(1, 3));
            Assert.Equal(0, samples.QuantizeProperty(2, -8));
            Assert.Equal(1, samples.QuantizeProperty(2, -7));
            Assert.Equal(2, samples.FindPropertyIndex(6));

            Span<int> properties = [0, 0, 0, 0, 0, 0, -8];
            Span<int> predictions = stackalloc int[JxlPredictorFacts.ModularPredictors];
            predictions[(int)JxlPredictor.Weighted] = 0;
            predictions[(int)JxlPredictor.Gradient] = 1;
            samples.AddSample(2, properties, predictions);
            samples.AddSample(2, properties, predictions);

            // Residuals 2 and 1 encode directly as tokens 4 and 2. Changing only Gradient must retain a different sample.
            predictions[(int)JxlPredictor.Gradient] = 0;
            samples.AddSample(2, properties, predictions);
            predictions[(int)JxlPredictor.Gradient] = 1;
            properties[0] = 1;
            samples.AddSample(2, properties, predictions);
            properties[0] = 0;
            properties[6] = 8;
            samples.AddSample(2, properties, predictions);

            Assert.Equal(5, samples.NumberOfSamples);
            Assert.Equal(4, samples.NumberOfDistinctSamples);
            Assert.Equal(2, samples.GetCount(0));
            Assert.Equal(1, samples.GetCount(1));
            Assert.Equal(4, samples.GetResidualTokensForPrediction(0).Length);
            Assert.Equal(4, samples.GetToken(0, 0));
            Assert.Equal(2, samples.GetToken(1, 0));
            Assert.Equal(4, samples.GetToken(1, 1));
            Assert.Equal(1, samples.GetProperty(true, 0, 2));
            Assert.Equal(1, samples.GetProperty(false, 0, 3));

            samples.AllSamplesDone();
            samples.Swap(0, 3);
            Assert.Equal(1, samples.GetCount(0));
            Assert.Equal(2, samples.GetCount(3));
            Assert.Equal(1, samples.GetProperty(false, 0, 0));
            Assert.Equal(0, samples.GetProperty(false, 0, 3));
        }
        finally
        {
            samples.Dispose();
        }
    }

    /// <summary>
    /// Checks native count saturation: the next matching sample starts a new record after the first count reaches 65535.
    /// </summary>
    [Fact]
    public void TreeSamples_SaturatedCountsStartANewRecord()
    {
        JxlTreeSamples samples = new(TestEnvironment.Configuration);
        try
        {
            samples.SetPredictor(JxlPredictor.Zero, JxlTreeMode.Default);
            samples.SetProperties([2], JxlTreeMode.Default);
            samples.PrepareForSamples(ushort.MaxValue + 1);
            samples.PreQuantizeProperties(default, [], [], [], [], [], 2);
            ReadOnlySpan<int> properties = [0, 0, 0];
            ReadOnlySpan<int> predictions = [0];
            for (int i = 0; i <= ushort.MaxValue; i++)
            {
                samples.AddSample(1, properties, predictions);
            }

            Assert.Equal(ushort.MaxValue + 1, samples.NumberOfSamples);
            Assert.Equal(2, samples.NumberOfDistinctSamples);
            Assert.Equal(ushort.MaxValue, samples.GetCount(0));
            Assert.Equal(1, samples.GetCount(1));
        }
        finally
        {
            samples.Dispose();
        }
    }

    /// <summary>
    /// Checks that excluding weighted prediction changes the owned selection without modifying caller property IDs.
    /// </summary>
    [Fact]
    public void TreeSamples_NoWeightedModeDoesNotChangeOptions()
    {
        Span<int> selected = [0, 1, JxlMaConstants.WpProp, 6];
        JxlTreeSamples samples = new(TestEnvironment.Configuration);
        try
        {
            samples.SetPredictor(JxlPredictor.Best, JxlTreeMode.NoWp);
            samples.SetProperties(selected, JxlTreeMode.NoWp);
            Assert.Equal(1, samples.NumberOfPredictors);
            Assert.Equal(JxlPredictor.Gradient, samples.PredictorFromIndex(0));
            Assert.Equal(3, samples.NumberOfProperties);
            Assert.Equal(6, samples.PropertyFromIndex(2));
            Assert.Equal(JxlMaConstants.WpProp, selected[2]);
            Assert.Equal(6, selected[3]);
        }
        finally
        {
            samples.Dispose();
        }
    }

    /// <summary>
    /// Checks terminal zero-bit and one-bit Huffman symbols through the encoded table and bounded bit reader.
    /// </summary>
    /// <param name="wire">The fixed simple-code header and final symbol bits.</param>
    /// <param name="headerBits">The exact header length.</param>
    /// <param name="codeBits">The number of bits in each decoded code.</param>
    /// <param name="first">The first decoded symbol.</param>
    /// <param name="second">The second decoded symbol.</param>
    [Theory]
    [InlineData((byte)0x11, 5, 0, (ushort)1, (ushort)1)]
    [InlineData((byte)0xA5, 6, 1, (ushort)0, (ushort)1)]
    public void HuffmanSymbols_ConsumeOnlyEncodedBitsAtEnd(byte wire, int headerBits, int codeBits, ushort first, ushort second)
    {
        // 0x11 declares the sole symbol 1, whose code needs no bits. 0xA5 declares symbols 0 and 1, followed by codes 0 and 1.
        using MemoryStream input = new([wire]);
        JxlBitReader reader = new(input);
        JxlHuffmanDecoder decoder = new();

        Assert.True(decoder.ReadFromBitStream(2, reader));
        Assert.Equal(headerBits, reader.TotalBitsConsumed);

        if (codeBits == 0)
        {
            // Move past the unused byte padding. A zero-bit symbol remains valid with no input left to peek.
            reader.SkipBits32((uint)(8 - headerBits));
        }

        long before = reader.TotalBitsConsumed;
        Assert.Equal(first, decoder.ReadSymbol(reader));
        Assert.Equal(codeBits, reader.TotalBitsConsumed - before);
        Assert.Equal(second, decoder.ReadSymbol(reader));
        Assert.Equal(8, reader.TotalBitsConsumed);
        Assert.Throws<EndOfStreamException>(() => reader.ReadBoolean());

        if (codeBits != 0)
        {
            // Zero-filled lookahead must not let the next one-bit code consume a nonexistent bit.
            Assert.Throws<EndOfStreamException>(() => decoder.ReadSymbol(reader));
        }
    }

    /// <summary>
    /// Checks reference records when fewer matching channels exist than the requested property capacity.
    /// </summary>
    [Fact]
    public void PredictionReferences_StopAtFirstChannelAndClearUnusedProperties()
    {
        using JxlModularImage image = new(2, 1, 8);
        image.Channels.Add(new(TestEnvironment.Configuration, 2, 1, 0, 0));
        image.Channels.Add(new(TestEnvironment.Configuration, 2, 1, 0, 0));
        image.Channels.Add(new(TestEnvironment.Configuration, 2, 1, 0, 0));
        Span<int> source = image.Channels[0].GetRow(0);
        source[0] = -2;
        source[1] = 3;

        using JxlModularChannel references = new(TestEnvironment.Configuration, 8, 2, 0, 0);
        Span<int> records = references.GetRow(0);
        records.Fill(99);
        JxlContextPrediction.PrecomputeReferences(image.Channels[1], 0, image, 1, references, records);

        // Each record is [abs(value), value, abs(residual), residual]. Row-zero prediction uses west, initially zero.
        // Only one earlier channel exists, so the second four-property group remains zero.
        ReadOnlySpan<int> first = [2, -2, 2, -2, 0, 0, 0, 0];
        ReadOnlySpan<int> second = [3, 3, 5, 5, 0, 0, 0, 0];
        Assert.Equal(first, records[..8]);
        Assert.Equal(second, records.Slice(references.Plane.PixelsPerRow, 8));

        Span<int> next = image.Channels[1].GetRow(0);
        next[0] = 4;
        next[1] = -1;
        JxlContextPrediction.PrecomputeReferences(image.Channels[2], 0, image, 2, references, records);

        // References visit the nearest earlier channel first. Both property groups must fit through the final pixel.
        ReadOnlySpan<int> firstWithTwoReferences = [4, 4, 4, 4, 2, -2, 2, -2];
        ReadOnlySpan<int> secondWithTwoReferences = [1, -1, 5, -5, 3, 3, 5, 5];
        Assert.Equal(firstWithTwoReferences, records[..8]);
        Assert.Equal(secondWithTwoReferences, records.Slice(references.Plane.PixelsPerRow, 8));

        JxlContextPrediction.PrecomputeReferences(image.Channels[0], 0, image, 0, references, records);

        // The first channel has no earlier references. Preparing it must clear the previous records without indexing channel -1.
        ReadOnlySpan<int> empty = [0, 0, 0, 0, 0, 0, 0, 0];
        Assert.Equal(empty, records[..8]);
        Assert.Equal(empty, records.Slice(references.Plane.PixelsPerRow, 8));
    }

    /// <summary>
    /// Checks the fixed-point error-weight approximation at zero and at unsigned error boundaries.
    /// </summary>
    /// <param name="error">The unsigned accumulated prediction error.</param>
    /// <param name="maximumWeight">The four-bit weight from the prediction header.</param>
    /// <param name="expected">The independently calculated fixed-point weight.</param>
    [Theory]
    [InlineData(0U, 13U, 218103812U)]
    [InlineData(16777215U, 13U, 17U)]
    [InlineData(2147483648U, 13U, 4U)]
    [InlineData(uint.MaxValue, 13U, 4U)]
    public void WeightedPrediction_ErrorWeightPreservesUnsignedErrors(uint error, uint maximumWeight, uint expected)
    {
        // Zero uses 13*2^24+4. At 2^24-1 the scaled lookup gives 13+4; the larger errors reduce to the minimum weight four.
        // The largest error must be widened before adding one, rather than becoming signed -1 and then zero.
        Assert.Equal(expected, JxlModularState.ErrorWeight(error, maximumWeight));
    }

    /// <summary>
    /// Checks signed-error selection, independent channel histories and header rebinding when storage is reused.
    /// </summary>
    [Fact]
    public void WeightedPrediction_SelectsLargestErrorAndResetsHistories()
    {
        static void RunTest()
        {
            JxlModularHeader header = new()
            {
                P1C = 0,
                P2C = 0,
                P3Ca = 0,
                P3Cb = 0,
                P3Cc = 0,
                P3Cd = 0,
                P3Ce = 0
            };

            // Zero header weights give four equal weights of four. These are valid custom header values.
            header.GetW().Clear();
            using JxlModularState state = new(TestEnvironment.Configuration, header, 3, 2);
            Span<uint> storage = state.Reset(header, 3);
            ReadOnlySpan<long> firstRow = [10, 0, 5];

            for (int x = 0; x < firstRow.Length; x++)
            {
                long left = x == 0 ? 0 : firstRow[x - 1];
                _ = state.Predict(false, x, 0, storage, 0, left, left, left, left, left, [], 0);
                state.UpdatePredictionErrors(firstRow[x], x, 0, storage, 0);

                // A second interleaved channel stays zero. It must not replace the first channel's predictions or error history.
                _ = state.Predict(false, x, 0, storage, 1, 0, 0, 0, 0, 0, [], 0);
                state.UpdatePredictionErrors(0, x, 0, storage, 1);
            }

            // The first row's signed errors are -80, +80 and -40 in eighth-sample units.
            // The next prediction is 60/8, rounded to seven; storing seven leaves west error +4.
            Assert.Equal(7L, state.Predict(false, 0, 1, storage, 0, 10, 10, 0, 10, 10, [], 0));
            state.UpdatePredictionErrors(7, 0, 1, storage, 0);
            _ = state.Predict(false, 0, 1, storage, 1, 0, 0, 0, 0, 0, [], 0);
            state.UpdatePredictionErrors(0, 0, 1, storage, 1);
            Span<int> properties = stackalloc int[2];
            _ = state.Predict(true, 1, 1, storage, 0, 0, 7, 5, 10, 0, properties, 0);
            _ = state.Predict(true, 1, 1, storage, 1, 0, 0, 0, 0, 0, properties, 1);

            // +80 is the first greatest magnitude. The equal -80 and smaller -40 must not replace it merely because both exceed west's +4.
            Assert.Equal(80, properties[0]);
            Assert.Equal(0, properties[1]);

            JxlModularHeader replacement = new()
            {
                P1C = 0,
                P2C = 31,
                P3Ca = 0,
                P3Cb = 0,
                P3Cc = 0,
                P3Cd = 0,
                P3Ce = 0
            };

            replacement.GetW().Clear();
            storage = state.Reset(replacement, 3);

            for (int x = 0; x < firstRow.Length; x++)
            {
                long left = x == 0 ? 0 : firstRow[x - 1];
                _ = state.Predict(x == 0, x, 0, storage, 0, left, left, left, left, left, properties, 0);
                if (x == 0)
                {
                    Assert.Equal(0, properties[0]);
                }

                state.UpdatePredictionErrors(firstRow[x], x, 0, storage, 0);
            }

            // P2C=31 changes the west candidate to 235 eighth-sample units. The resulting prediction clamps to north's ten.
            Assert.Equal(10L, state.Predict(false, 0, 1, storage, 0, 10, 10, 0, 10, 10, [], 0));
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(RunTest, HwIntrinsics.AllowAll | HwIntrinsics.DisableHWIntrinsic);
    }

    [Fact]
    public void PackUnpack()
    {
        for (int i = -31; i < 32; i++)
        {
            uint packed = JxlPackSigned.PackUnsigned(i);

            Assert.True(packed < 63u);

            int unpacked = JxlPackSigned.UnpackSigned(packed);

            Assert.Equal(i, unpacked);
        }
    }

    /// <summary>
    /// Checks hybrid integers through one real bitstream, including each token's exact number of remaining bits.
    /// </summary>
    /// <param name="config">The split and token-bit settings.</param>
    /// <param name="limit">The largest generated integer.</param>
    private static void HybridUintRoundtrip(JxlAnsHybridUIntConfiguration config, uint limit)
    {
        Rng rng = new(0);

        const int numIntegers = 1 << 20;

        uint[] integers = new uint[numIntegers];
        uint[] token = new uint[numIntegers];
        uint[] nbits = new uint[numIntegers];
        using MemoryStream input = new();
        JxlBitWriter writer = new(input);

        for (int i = 0; i < numIntegers; i++)
        {
            integers[i] = (uint)rng.UniformU(0, limit + 1);
            config.Encode(
                integers[i],
                out token[i],
                out nbits[i],
                out uint bits);

            // Tokens belong to the entropy stream. Only their remaining low bits go into this bitstream.
            writer.Write((int)nbits[i], bits);
        }

        writer.ZeroPadToByte();
        input.Position = 0;
        JxlBitReader reader = new(input);

        for (int i = 0; i < numIntegers; i++)
        {
            long before = reader.TotalBitsConsumed;

            Assert.Equal(
                integers[i],
                (uint)JxlAnsSymbolReader.ReadHybridUIntConfiguration(config, (int)token[i], reader));

            Assert.Equal((long)nbits[i], reader.TotalBitsConsumed - before);
        }
    }

    [Fact]
    public void Test000() => HybridUintRoundtrip(new JxlAnsHybridUIntConfiguration(0, 0, 0), 1U << 24);

    [Fact]
    public void Test411() => HybridUintRoundtrip(new JxlAnsHybridUIntConfiguration(4, 1, 1), 1U << 24);

    [Fact]
    public void Test420() => HybridUintRoundtrip(new JxlAnsHybridUIntConfiguration(4, 2, 0), 1U << 24);

    [Fact]
    public void Test421() => HybridUintRoundtrip(
            new JxlAnsHybridUIntConfiguration(4, 2, 1),
            256);
}
