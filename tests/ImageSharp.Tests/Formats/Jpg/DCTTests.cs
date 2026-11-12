// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Tests.Formats.Jpg.Utils;
using SixLabors.ImageSharp.Tests.TestUtilities;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg;

[Trait("Format", "Jpg")]
public static class DCTTests
{
    // size of input values is 10 bit max
    private const float MaxInputValue = 1023;
    private const float MinInputValue = -1024;

    // output value range is 12 bit max
    private const float MaxOutputValue = 4096;
    private const float NormalizationValue = MaxOutputValue / 2;

    // The decoder stage adds the level shift ceiling(maximum / 2) and clamps to [0, maximum].
    // With this maximum, all test outputs stay inside the range. The tests then remove the level shift.
    private const float UnclampedMaximum = 65535F;
    private const float UnclampedLevelShift = 32768F;

    internal static Block8x8F CreateBlockFromScalar(float value)
    {
        Block8x8F result = default;
        for (int i = 0; i < Block8x8F.Size; i++)
        {
            result[i] = value;
        }

        return result;
    }

    /// <summary>
    /// Runs the decoder stage on one block and removes the level shift from the output.
    /// </summary>
    /// <param name="coefficients">The coefficients in natural order.</param>
    /// <param name="dequantizationTable">The dequantization table, adjusted by <see cref="FloatingPointDCT.AdjustToIDCT"/>.</param>
    /// <returns>The 8x8 inverse transform in natural order.</returns>
    private static Block8x8F InverseTransformWithoutLevelShift(int[] coefficients, ref Block8x8F dequantizationTable)
    {
        // The stage reads the coefficients in transposed order.
        short[] data = new short[Block8x8.Size];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (short)coefficients[i];
        }

        ReferenceImplementations.Transpose8x8(data);
        Block8x8 source = Block8x8.Load(data);

        float[] samples = new float[Block8x8F.Size];
        FloatingPointDCT.TransformIDCT(ref source, ref dequantizationTable, UnclampedMaximum, ref samples[0], Block8x8F.RowCount);

        Block8x8F result = default;
        for (int i = 0; i < Block8x8F.Size; i++)
        {
            result[i] = samples[i] - UnclampedLevelShift;
        }

        return result;
    }

    public class FastFloatingPoint : JpegFixture
    {
        public FastFloatingPoint(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void LLM_TransformIDCT_CompareToNonOptimized(int seed)
        {
            // JPEG coefficients are integers. The decoder stage reads them as 16-bit values.
            int[] sourceArray = Create8x8RandomIntData((int)MinInputValue, (int)MaxInputValue, seed);

            Block8x8F srcBlock = default;
            srcBlock.LoadFrom(sourceArray);

            // reference
            Block8x8F expected = ReferenceImplementations.LLM_FloatingPoint_DCT.TransformIDCT(ref srcBlock);

            // testee
            // Part of the IDCT calculations is fused into the quantization step
            // Dequantization using unit matrix - no values are upscaled
            Block8x8F dequantMatrix = CreateBlockFromScalar(1);

            // This step is needed to apply adjusting multipliers to the input block
            FloatingPointDCT.AdjustToIDCT(ref dequantMatrix);

            Block8x8F actual = InverseTransformWithoutLevelShift(sourceArray, ref dequantMatrix);

            this.CompareBlocks(expected, actual, 1f);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void LLM_TransformIDCT_CompareToAccurate(int seed)
        {
            // JPEG coefficients are integers. The decoder stage reads them as 16-bit values.
            int[] sourceArray = Create8x8RandomIntData((int)MinInputValue, (int)MaxInputValue, seed);

            Block8x8F srcBlock = default;
            srcBlock.LoadFrom(sourceArray);

            // reference
            Block8x8F expected = ReferenceImplementations.AccurateDCT.TransformIDCT(ref srcBlock);

            // testee
            // Part of the IDCT calculations is fused into the quantization step
            // Dequantization using unit matrix - no values are upscaled
            Block8x8F dequantMatrix = CreateBlockFromScalar(1);

            // This step is needed to apply adjusting multipliers to the input block
            FloatingPointDCT.AdjustToIDCT(ref dequantMatrix);

            Block8x8F actual = InverseTransformWithoutLevelShift(sourceArray, ref dequantMatrix);

            this.CompareBlocks(expected, actual, 1f);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void TransformIDCT(int seed)
        {
            static void RunTest(string serialized)
            {
                int seed = FeatureTestRunner.Deserialize<int>(serialized);

                // JPEG coefficients are integers. The decoder stage reads them as 16-bit values.
                int[] sourceArray = Create8x8RandomIntData((int)MinInputValue, (int)MaxInputValue, seed);
                float[] src = Array.ConvertAll(sourceArray, static v => (float)v);

                float[] expectedDest = new float[64];
                float[] temp = new float[64];

                // reference
                ReferenceImplementations.LLM_FloatingPoint_DCT.IDCT2D_llm(src, expectedDest, temp);

                // testee
                // Part of the IDCT calculations is fused into the quantization step
                // Dequantization using unit matrix - no values are upscaled
                // as quant matrix is all 1's
                Block8x8F dequantMatrix = CreateBlockFromScalar(1);

                // This step is needed to apply adjusting multipliers to the input block
                FloatingPointDCT.AdjustToIDCT(ref dequantMatrix);

                float[] actualDest = InverseTransformWithoutLevelShift(sourceArray, ref dequantMatrix).ToArray();

                Assert.Equal(actualDest, expectedDest, new ApproximateFloatComparer(1f));
            }

            // The test runs four feature sets:
            // 1. AllowAll: the Vector256 path. A single-block call never uses the Vector512 path.
            // 2. DisableAVX512F: the Vector256 path.
            // 3. DisableAVX: the Vector128 path.
            // 4. DisableHWIntrinsic: the scalar path.
            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                RunTest,
                seed,
                HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
        }

        [Fact]
        public void TransformIDCT_DcOnlyBlock_SetsAllElementsToDc()
        {
            // A block without AC coefficients gives the dequantized DC at every sample.
            static void RunTest()
            {
                Block8x8F dequantMatrix = CreateBlockFromScalar(1);
                FloatingPointDCT.AdjustToIDCT(ref dequantMatrix);

                int[] coefficients = new int[Block8x8F.Size];
                for (int dc = -2048; dc <= 2047; dc++)
                {
                    coefficients[0] = dc;
                    float expected = dc * dequantMatrix[0];

                    Block8x8F block = InverseTransformWithoutLevelShift(coefficients, ref dequantMatrix);

                    for (int i = 0; i < Block8x8F.Size; i++)
                    {
                        Assert.Equal(expected, block[i]);
                    }
                }
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                RunTest,
                HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
        }

        [Fact]
        public void TransformIDCT_ClampsToSampleRange()
        {
            // The decoder stage adds the level shift to the samples and clamps them to [0, maximum].
            static void RunTest()
            {
                int[] coefficients = Create8x8RandomIntData((int)MinInputValue, (int)MaxInputValue, 7);
                Block8x8 source = default;
                source.LoadFrom(coefficients);

                Block8x8F dequantMatrix = CreateBlockFromScalar(16);
                FloatingPointDCT.AdjustToIDCT(ref dequantMatrix);

                float[] samples = new float[Block8x8F.Size];
                FloatingPointDCT.TransformIDCT(ref source, ref dequantMatrix, 255F, ref samples[0], Block8x8F.RowCount);

                Assert.Contains(samples, static v => v == 0F);
                Assert.Contains(samples, static v => v == 255F);
                Assert.All(samples, static v => Assert.InRange(v, 0F, 255F));
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                RunTest,
                HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
        }

        [Fact]
        public void TransformIDCT_Row_EqualsOneCallForEachBlock()
        {
            // The row methods document the same result as one call for each block.
            // The Vector512 path transforms two blocks at a time. Thus, this test compares it with the single-block path.
            static void RunTest()
            {
                const int BlockCount = 5;
                const int Stride = 8 * BlockCount;

                Block8x8[] blocks = new Block8x8[BlockCount];
                for (int b = 0; b < BlockCount; b++)
                {
                    int[] coefficients = Create8x8RandomIntData((int)MinInputValue, (int)MaxInputValue, b + 1);

                    // Block 1 has only a DC coefficient. Thus, the row path also takes the DC shortcut.
                    if (b == 1)
                    {
                        Array.Clear(coefficients, 1, coefficients.Length - 1);
                    }

                    blocks[b].LoadFrom(coefficients);
                }

                Block8x8F fullTable = CreateBlockFromScalar(2);
                FloatingPointDCT.AdjustToIDCT(ref fullTable);
                Block8x8F scaledTable = CreateBlockFromScalar(2);
                ScaledFloatingPointDCT.AdjustToIDCT(ref scaledTable);

                AssertRowEqualsBlocks(
                    blocks,
                    8,
                    Stride,
                    (span, d, clear) => FloatingPointDCT.TransformIDCT(span, ref fullTable, MaxOutputValue, ref d[0], Stride, 1, 1, clear),
                    (ref b, d, o) => FloatingPointDCT.TransformIDCT(ref b, ref fullTable, MaxOutputValue, ref d[o], Stride));

                AssertRowEqualsBlocks(
                    blocks,
                    4,
                    Stride,
                    (span, d, clear) => ScaledFloatingPointDCT.TransformIDCT4x4(span, ref scaledTable, MaxOutputValue, ref d[0], Stride, 1, 1, clear),
                    (ref b, d, o) => ScaledFloatingPointDCT.TransformIDCT4x4(ref b, ref scaledTable, MaxOutputValue, ref d[o], Stride));

                AssertRowEqualsBlocks(
                    blocks,
                    2,
                    Stride,
                    (span, d, clear) => ScaledFloatingPointDCT.TransformIDCT2x2(span, ref scaledTable, MaxOutputValue, ref d[0], Stride, 1, 1, clear),
                    (ref b, d, o) => ScaledFloatingPointDCT.TransformIDCT2x2(ref b, ref scaledTable, MaxOutputValue, ref d[o], Stride));
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                RunTest,
                HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void TranformIDCT_4x4(int seed)
        {
            // JPEG coefficients are integers. CreateTransposedCoefficients rounds the values and stores them as 16-bit values.
            float[] src = Create8x8RandomFloatData(MinInputValue, MaxInputValue, seed, 4, 4);
            Block8x8 source = CreateTransposedCoefficients(src);

            float[] expectedDest = new float[64];
            float[] temp = new float[64];

            // reference
            ReferenceImplementations.LLM_FloatingPoint_DCT.IDCT2D_llm(src, expectedDest, temp);

            // testee
            // Part of the IDCT calculations is fused into the quantization step
            // We must multiply input block with adjusted no-quantization matrix
            // before applying IDCT
            Block8x8F dequantMatrix = CreateBlockFromScalar(1);

            // Dequantization using unit matrix - no values are upscaled
            // as quant matrix is all 1's
            // This step is needed to apply adjusting multipliers to the input block
            ScaledFloatingPointDCT.AdjustToIDCT(ref dequantMatrix);

            float[] actualDest = new float[64];
            ScaledFloatingPointDCT.TransformIDCT4x4(ref source, ref dequantMatrix, MaxOutputValue, ref actualDest[0], Block8x8F.RowCount);

            Span<float> expectedSpan = expectedDest.AsSpan();
            Span<float> actualSpan = actualDest.AsSpan();

            // resulting matrix is 4x4
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    AssertScaledElementEquality(expectedSpan[((y * 16) + (x * 2))..], actualSpan[((y * 8) + x)..]);
                }
            }

            static void AssertScaledElementEquality(Span<float> expected, Span<float> actual)
            {
                float average2x2 = 0f;
                for (int y = 0; y < 2; y++)
                {
                    int y8 = y * 8;
                    for (int x = 0; x < 2; x++)
                    {
                        float clamped = ColorNumerics.ClampToFinite(expected[y8 + x] + NormalizationValue, 0, MaxOutputValue);
                        average2x2 += clamped;
                    }
                }

                average2x2 /= 4f;

                Assert.Equal((int)average2x2, (int)actual[0]);
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void TranformIDCT_2x2(int seed)
        {
            // JPEG coefficients are integers. CreateTransposedCoefficients rounds the values and stores them as 16-bit values.
            float[] src = Create8x8RandomFloatData(MinInputValue, MaxInputValue, seed, 2, 2);
            Block8x8 source = CreateTransposedCoefficients(src);

            float[] expectedDest = new float[64];
            float[] temp = new float[64];

            // reference
            ReferenceImplementations.LLM_FloatingPoint_DCT.IDCT2D_llm(src, expectedDest, temp);

            // testee
            // Part of the IDCT calculations is fused into the quantization step
            // We must multiply input block with adjusted no-quantization matrix
            // before applying IDCT
            Block8x8F dequantMatrix = CreateBlockFromScalar(1);

            // Dequantization using unit matrix - no values are upscaled
            // as quant matrix is all 1's
            // This step is needed to apply adjusting multipliers to the input block
            ScaledFloatingPointDCT.AdjustToIDCT(ref dequantMatrix);

            float[] actualDest = new float[64];
            ScaledFloatingPointDCT.TransformIDCT2x2(ref source, ref dequantMatrix, MaxOutputValue, ref actualDest[0], Block8x8F.RowCount);

            Span<float> expectedSpan = expectedDest.AsSpan();
            Span<float> actualSpan = actualDest.AsSpan();

            // resulting matrix is 2x2
            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 2; x++)
                {
                    AssertScaledElementEquality(expectedSpan[((y * 32) + (x * 4))..], actualSpan[((y * 8) + x)..]);
                }
            }

            static void AssertScaledElementEquality(Span<float> expected, Span<float> actual)
            {
                float average4x4 = 0f;
                for (int y = 0; y < 4; y++)
                {
                    int y8 = y * 8;
                    for (int x = 0; x < 4; x++)
                    {
                        float clamped = ColorNumerics.ClampToFinite(expected[y8 + x] + NormalizationValue, 0, MaxOutputValue);
                        average4x4 += clamped;
                    }
                }

                average4x4 /= 16f;

                Assert.Equal((int)average4x4, (int)actual[0]);
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void TranformIDCT_1x1(int seed)
        {
            // JPEG coefficients are integers. CreateTransposedCoefficients rounds the values and stores them as 16-bit values.
            float[] src = Create8x8RandomFloatData(MinInputValue, MaxInputValue, seed, 1, 1);
            Block8x8[] source = [CreateTransposedCoefficients(src)];

            float[] expectedDest = new float[64];
            float[] temp = new float[64];

            // reference
            ReferenceImplementations.LLM_FloatingPoint_DCT.IDCT2D_llm(src, expectedDest, temp);

            // testee
            // Part of the IDCT calculations is fused into the quantization step
            // Dequantization using unit matrix - no values are upscaled
            // as quant matrix is all 1's
            Block8x8F dequantMatrix = CreateBlockFromScalar(1);

            // This step is needed to apply adjusting multipliers to the input block
            ScaledFloatingPointDCT.AdjustToIDCT(ref dequantMatrix);

            // The 8x8 to 1x1 transform writes one sample for each block.
            float[] actualDest = new float[1];
            ScaledFloatingPointDCT.TransformIDCT1x1(source, ref dequantMatrix, MaxOutputValue, ref actualDest[0], 1, 1, 1, false);

            float expected = ColorNumerics.ClampToFinite(expectedDest[0] + NormalizationValue, 0, MaxOutputValue);

            Assert.Equal((int)expected, (int)actualDest[0]);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void TransformFDCT_QuantizesInZigZagOrder(int seed)
        {
            // The encoder stage subtracts the level shift from the samples and applies the forward transform.
            // Then it divides by the quantization table, rounds, and writes the coefficients in zig-zag order.
            static void RunTest(string serialized)
            {
                int seed = FeatureTestRunner.Deserialize<int>(serialized);

                float[] src = Create8x8RandomFloatData(MinInputValue, MaxInputValue, seed);

                // Reference: apply the forward transform, then divide each coefficient by the quantization table in zig-zag order.
                float[] expectedDest = new float[64];
                float[] temp = new float[64];
                ReferenceImplementations.LLM_FloatingPoint_DCT.FDCT2D_llm(src, expectedDest, temp, downscaleBy8: true);

                Random random = new(seed);
                Block8x8F quantizationTable = default;
                for (int i = 0; i < Block8x8F.Size; i++)
                {
                    quantizationTable[i] = random.Next(1, 256);
                }

                ReadOnlySpan<byte> zigZag = ZigZag.ZigZagOrder;
                int[] expected = new int[Block8x8F.Size];
                for (int i = 0; i < Block8x8F.Size; i++)
                {
                    expected[i] = (int)Math.Round(expectedDest[zigZag[i]] / quantizationTable[zigZag[i]]);
                }

                // Testee: the stage subtracts the level shift of 128. Thus, the test adds 128 to each sample first.
                float[] samples = Array.ConvertAll(src, static v => v + 128F);
                Block8x8F adjustedTable = quantizationTable;
                FloatingPointDCT.AdjustToFDCT(ref adjustedTable);

                Block8x8 actual = default;
                FloatingPointDCT.TransformFDCT(ref samples[0], Block8x8F.RowCount, ref adjustedTable, ref actual);

                for (int i = 0; i < Block8x8F.Size; i++)
                {
                    Assert.InRange(actual[i] - expected[i], -1, 1);
                }
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                RunTest,
                seed,
                HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
        }

        [Fact]
        public void TransformFDCT_Row_EqualsOneCallForEachBlock()
        {
            // The row method documents the same result as one call for each block.
            // The Vector512 path transforms two blocks at a time. Thus, this test compares it with the single-block path.
            static void RunTest()
            {
                const int BlockCount = 5;
                const int Stride = 8 * BlockCount;

                Random random = new(3);
                float[] samples = new float[Stride * 8];
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] = random.Next(0, 256);
                }

                Block8x8F quantizationTable = CreateBlockFromScalar(3);
                FloatingPointDCT.AdjustToFDCT(ref quantizationTable);

                Block8x8[] row = new Block8x8[BlockCount];
                FloatingPointDCT.TransformFDCT(ref samples[0], Stride, ref quantizationTable, row);

                for (int b = 0; b < BlockCount; b++)
                {
                    Block8x8 single = default;
                    FloatingPointDCT.TransformFDCT(ref samples[b * 8], Stride, ref quantizationTable, ref single);
                    Assert.Equal(single, row[b]);
                }
            }

            FeatureTestRunner.RunWithHwIntrinsicsFeature(
                RunTest,
                HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
        }

        /// <summary>
        /// Rounds the coefficients to integers and returns them in the transposed order of the decoder stage.
        /// </summary>
        /// <param name="coefficients">The coefficients in natural order. The method rounds them in place.</param>
        /// <returns>The transposed block.</returns>
        private static Block8x8 CreateTransposedCoefficients(float[] coefficients)
        {
            short[] data = new short[Block8x8.Size];
            for (int i = 0; i < data.Length; i++)
            {
                coefficients[i] = MathF.Round(coefficients[i]);
                data[i] = (short)coefficients[i];
            }

            ReferenceImplementations.Transpose8x8(data);
            return Block8x8.Load(data);
        }

        /// <summary>
        /// Checks that a row call gives the same samples, to the last bit, as one call for each block.
        /// It also checks that the call with clearSource leaves every block zero.
        /// </summary>
        /// <param name="blocks">The blocks.</param>
        /// <param name="size">The output size of the transform.</param>
        /// <param name="stride">The destination stride.</param>
        /// <param name="row">The row call.</param>
        /// <param name="single">The single-block call. Its third argument is the destination offset.</param>
        private static void AssertRowEqualsBlocks(Block8x8[] blocks, int size, int stride, RowTransform row, SingleTransform single)
        {
            float[] expected = new float[stride * 8];
            float[] actual = new float[stride * 8];
            float[] actualCleared = new float[stride * 8];
            Block8x8[] cleared = (Block8x8[])blocks.Clone();

            row(blocks, actual, false);
            row(cleared, actualCleared, true);
            for (int b = 0; b < blocks.Length; b++)
            {
                single(ref blocks[b], expected, b * size);
            }

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(BitConverter.SingleToInt32Bits(expected[i]), BitConverter.SingleToInt32Bits(actual[i]));
                Assert.Equal(BitConverter.SingleToInt32Bits(expected[i]), BitConverter.SingleToInt32Bits(actualCleared[i]));
            }

            // A baseline decoder decodes the next MCU row into the same blocks and writes only nonzero coefficients.
            // Thus, the call with clearSource must leave every block zero.
            for (int b = 0; b < cleared.Length; b++)
            {
                Assert.Equal(default, cleared[b]);
            }
        }

        private delegate void RowTransform(Span<Block8x8> blocks, float[] destination, bool clearSource);

        private delegate void SingleTransform(ref Block8x8 block, float[] destination, int offset);
    }
}
