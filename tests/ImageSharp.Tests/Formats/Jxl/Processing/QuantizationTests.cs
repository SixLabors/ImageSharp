// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Modular;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;
using SixLabors.ImageSharp.Tests.Memory;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing;

public class QuantizationTests
{
    /// <summary>
    /// Checks the adaptive quantizer's four-minimum filter and two-to-one rectangle traversal against independently sorted neighborhoods.
    /// </summary>
    [Fact]
    public void AdaptiveFuzzyErosion_DownsamplesRectanglesWithoutTouchingPadding()
    {
        static void RunTest()
        {
            // The last case places both regions inside larger planes. Neighbors use global source edges, not the selected rectangle.
            ReadOnlySpan<(int Width, int Height, Rectangle Source, Rectangle Destination)> cases =
            [
                (2, 2, new Rectangle(0, 0, 2, 2), new Rectangle(0, 0, 1, 1)),
                (6, 4, new Rectangle(0, 0, 6, 4), new Rectangle(1, 1, 3, 2)),
                (34, 6, new Rectangle(0, 0, 34, 6), new Rectangle(2, 1, 17, 3)),
                (66, 4, new Rectangle(0, 0, 66, 4), new Rectangle(1, 2, 33, 2)),
                (70, 8, new Rectangle(2, 2, 66, 4), new Rectangle(3, 1, 33, 2))
            ];

            const float untouched = -123F;
            // At target 2, native weights [.125,.1,.09,.06] sum to .375 before normalization. All four minima contribute.
            float normalization = 0.29959705784054957F / 0.375F;
            ReadOnlySpan<float> weights = [0.125F * normalization, 0.1F * normalization, 0.09F * normalization, 0.06F * normalization];
            Span<float> neighbors = stackalloc float[9];

            foreach ((int width, int height, Rectangle sourceRect, Rectangle destinationRect) in cases)
            {
                using JxlPlane<float> source = new(TestEnvironment.Configuration, width, height);
                using JxlPlane<float> destination = new(TestEnvironment.Configuration, destinationRect.Right + 2, destinationRect.Bottom + 2);

                Span<float> input = source.GetRow(0);
                Span<float> output = destination.GetRow(0);
                input.Fill(untouched);
                output.Fill(untouched);

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        input[(y * source.PixelsPerRow) + x] = ((x * 13 + y * 7) % 19) + 1;
                    }
                }

                Assert.True(JxlAdaptiveQuantization.FuzzyErosion(2F, sourceRect, source, destinationRect, destination));

                for (int y = 0; y < destination.YSize; y++)
                {
                    ReadOnlySpan<float> row = output.Slice(y * destination.PixelsPerRow, destination.PixelsPerRow);
                    for (int x = 0; x < row.Length; x++)
                    {
                        if (!destinationRect.Contains(x, y))
                        {
                            Assert.Equal(untouched, row[x]);
                            continue;
                        }

                        float expected = 0F;
                        for (int pairY = 0; pairY < 2; pairY++)
                        {
                            int sourceY = sourceRect.Top + (2 * (y - destinationRect.Top)) + pairY;
                            for (int pairX = 0; pairX < 2; pairX++)
                            {
                                int sourceX = sourceRect.Left + (2 * (x - destinationRect.Left)) + pairX;
                                int count = 0;
                                for (int dy = -1; dy <= 1; dy++)
                                {
                                    int neighborY = Math.Clamp(sourceY + dy, 0, height - 1);
                                    for (int dx = -1; dx <= 1; dx++)
                                    {
                                        int neighborX = Math.Clamp(sourceX + dx, 0, width - 1);
                                        neighbors[count++] = input[(neighborY * source.PixelsPerRow) + neighborX];
                                    }
                                }

                                // Full sorting is an independent oracle for the production four-minimum insertion network.
                                neighbors.Sort();
                                float value = (((weights[0] * neighbors[0]) + (weights[1] * neighbors[1]))
                                    + (weights[2] * neighbors[2])) + (weights[3] * neighbors[3]);

                                expected += value;
                            }
                        }

                        Assert.Equal(expected, row[x]);
                    }
                }
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Checks final table layout, reciprocal weights, low-frequency exclusion and output guards at every SIMD width.
    /// </summary>
    [Fact]
    public void QuantizationTables_WriteReciprocalsAndExcludeLowFrequencies()
    {
        static void RunTest()
        {
            using JxlQuantizerEncoding encoding = JxlQuantizerEncoding.Dct(new JxlDctQuantWeightParameters([2F, 4F, 8F], 1));
            ReadOnlySpan<JxlQuantTable> kinds = [JxlQuantTable.DCT, JxlQuantTable.DCT8X16, JxlQuantTable.DCT16X16, JxlQuantTable.DCT256X256];

            foreach (JxlQuantTable kind in kinds)
            {
                int blocksX = JxlDequantMatrices.RequiredSizeX[(int)kind];
                int blocksY = JxlDequantMatrices.RequiredSizeY[(int)kind];
                int channelLength = blocksX * blocksY * 64;
                int length = 3 * channelLength;
                float[] table = new float[length + 2];
                float[] inverse = new float[length + 2];
                table.AsSpan().Fill(-77F);
                inverse.AsSpan().Fill(-77F);
                int offset = 1;
                Assert.True(JxlQuantWeights.ComputeQuantTable(encoding, table, inverse, kind, ref offset));
                Assert.Equal(length + 1, offset);

                // One distance band gives constant weights [2,4,8]. Their exact reciprocals are [.5,.25,.125].
                // The top-left covered-block rectangle is excluded only from AC quantization, as specified by the native table layout.
                int columns = 8 * Math.Max(blocksX, blocksY);
                int lowRows = Math.Min(blocksX, blocksY);
                int lowColumns = Math.Max(blocksX, blocksY);
                for (int c = 0; c < 3; c++)
                {
                    float weight = 2 << c;
                    int start = 1 + (c * channelLength);
                    Assert.Equal(-1, table.AsSpan(start, channelLength).IndexOfAnyExcept(1F / weight));
                    for (int row = 0; row < channelLength / columns; row++)
                    {
                        Span<float> values = inverse.AsSpan(start + (row * columns), columns);
                        int excluded = row < lowRows ? lowColumns : 0;
                        Assert.Equal(-1, values[..excluded].IndexOfAnyExcept(0F));
                        Assert.Equal(-1, values[excluded..].IndexOfAnyExcept(weight));
                    }
                }

                Assert.Equal(-77F, table[0]);
                Assert.Equal(-77F, table[^1]);
                Assert.Equal(-77F, inverse[0]);
                Assert.Equal(-77F, inverse[^1]);
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Checks that invalid generated weights cannot publish a completed table at any SIMD width.
    /// </summary>
    [Fact]
    public void QuantizationTables_RejectNonfiniteAndOutOfRangeWeights()
    {
        static void RunTest()
        {
            ReadOnlySpan<float> invalidWeights = [1E8F, float.PositiveInfinity, float.NaN];
            foreach (float invalid in invalidWeights)
            {
                // A one-band table reaches the reciprocal validation directly. The third channel must still be checked.
                using JxlQuantizerEncoding encoding = JxlQuantizerEncoding.Dct(new JxlDctQuantWeightParameters([2F, 4F, invalid], 1));
                float[] table = new float[3 * 64];
                float[] inverse = new float[3 * 64];
                int offset = 0;
                Assert.Throws<InvalidImageContentException>(() => JxlQuantWeights.ComputeQuantTable(encoding, table, inverse, JxlQuantTable.DCT, ref offset));
                Assert.Equal(0, offset);
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Checks the native default band count and the rectangular transform's predefined table selection.
    /// </summary>
    [Fact]
    public void DefaultWeights_MatchNativeBandAndTableMetadata()
    {
        Assert.Equal(8, JxlQuantWeights.Dct32x32.DctParameters.NumDistanceBands);
        JxlQuantizerEncoding expected = JxlQuantWeights.Dct32x64;
        JxlQuantizerEncoding actual = JxlDequantMatrices.Library[(int)JxlQuantTable.DCT32X64];
        Assert.NotSame(expected, actual);
        Assert.Equal(expected.Mode, actual.Mode);
        Assert.Equal(expected.DctParameters.NumDistanceBands, actual.DctParameters.NumDistanceBands);
        for (int c = 0; c < 3; c++)
        {
            ReadOnlySpan<float> expectedBands = expected.DctParameters.DistanceBands[c];
            ReadOnlySpan<float> actualBands = actual.DctParameters.DistanceBands[c];
            Assert.True(expectedBands.SequenceEqual(actualBands));
        }

        // These are the default decimal seed products rounded once to float, as native quant_weights.cc defines them.
        // Checking the exact bits catches rounding operands before multiplication, without changing interpolation tolerances.
        ReadOnlySpan<JxlQuantizerEncoding> largeTables =
        [
            JxlQuantWeights.Dct64x64, JxlQuantWeights.Dct32x64,
            JxlQuantWeights.Dct128x128, JxlQuantWeights.Dct64x128,
            JxlQuantWeights.Dct256x256, JxlQuantWeights.Dct128x256
        ];

        ReadOnlySpan<int> seedBits =
        [
            0x46BB3C55, 0x4602F0C4, 0x458C6831,
            0x466FFB98, 0x45AEEAE2, 0x45367F63,
            0x473B3C55, 0x4682F0C4, 0x460C6831,
            0x46EFFB98, 0x462EEAE2, 0x45B67F63,
            0x47BB3C55, 0x4702F0C4, 0x468C6831,
            0x476FFB98, 0x46AEEAE2, 0x46367F63
        ];

        for (int table = 0; table < largeTables.Length; table++)
        {
            for (int c = 0; c < 3; c++)
            {
                Assert.Equal(seedBits[(table * 3) + c], BitConverter.SingleToInt32Bits(largeTables[table].DctParameters.DistanceBands[c][0]));
            }
        }
    }

    /// <summary>
    /// Checks native signed band multipliers, exact constant intervals, independent channel layout, and all SIMD tails.
    /// </summary>
    [Fact]
    public void DistanceWeights_ApplyMultipliersAndPreserveExactIntervalValues()
    {
        static void RunTest()
        {
            // Mult(0)=1 keeps every interval constant. This exercises the real interpolation path rather than the single-band fill.
            JxlDctQuantWeightParameters constantBands = new([2F, 0F, 0F, 4F, 0F, 0F, 8F, 0F, 0F], 3);
            JxlDctQuantWeightParameters varyingBands = new([2F, 1F, -1F, 4F, 1F, -1F, 8F, 1F, -1F], 3);
            ReadOnlySpan<int> columns = [3, 4, 7, 8, 15, 16, 17, 31, 32, 33];

            foreach (int columnCount in columns)
            {
                const int rowCount = 3;
                int channelSize = rowCount * columnCount;
                float[] storage = new float[(3 * channelSize) + 2];
                storage.AsSpan().Fill(-77F);
                Span<float> weights = storage.AsSpan(1, 3 * channelSize);
                Assert.True(JxlQuantWeights.GetQuantWeights(rowCount, columnCount, in constantBands, weights));

                for (int c = 0; c < 3; c++)
                {
                    for (int i = 0; i < channelSize; i++)
                    {
                        Assert.Equal(constantBands.DistanceBands[c][0], weights[(c * channelSize) + i]);
                    }
                }

                Assert.Equal(-77F, storage[0]);
                Assert.Equal(-77F, storage[^1]);

                // Mult(1)=2 and Mult(-1)=1/2 produce positive cumulative bands [a,2a,a]. Raw multiplication would reject them.
                Assert.True(JxlQuantWeights.GetQuantWeights(rowCount, columnCount, in varyingBands, weights));
                for (int c = 0; c < 3; c++)
                {
                    Assert.Equal(varyingBands.DistanceBands[c][0], weights[c * channelSize]);
                }

                Assert.Equal(-77F, storage[0]);
                Assert.Equal(-77F, storage[^1]);
            }

            // Scaled positions zero and one are the exact starts of successive geometric intervals.
            ReadOnlySpan<float> samples = [2F, 8F, 4F];
            ReadOnlySpan<float> logarithms = [2F, -1F];
            Assert.Equal(2F, JxlQuantWeights.Interpolate(0F, samples, logarithms));
            Assert.Equal(8F, JxlQuantWeights.Interpolate(1F, samples, logarithms));
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Checks half-row thresholds and ties-to-even integer rounding for narrow and full-width coefficient rows.
    /// </summary>
    [Fact]
    public void Quantize_UsesQuadrantThresholdsAndEvenMidpoints()
    {
        static void RunTest()
        {
            ReadOnlySpan<float> samples = [-3.5F, -2.5F, -1.5F, -1.25F, -1.125F, -0.5F, 0F, 0.5F, 1.125F, 1.25F, 1.5F, 2.5F, 3.5F];
            ReadOnlySpan<int> lowThreshold = [-4, -2, -2, -1, -1, 0, 0, 0, 1, 1, 2, 2, 4];
            ReadOnlySpan<int> highThreshold = [-4, -2, -2, -1, 0, 0, 0, 0, 0, 1, 2, 2, 4];
            ReadOnlySpan<int> widths = [8, 16, 32, 64, 128, 256];

            foreach (int width in widths)
            {
                float[] source = new float[width + 1];
                float[] matrix = new float[width + 1];
                int[] output = new int[width + 2];
                matrix.AsSpan().Fill(2F);
                output.AsSpan().Fill(-777);

                for (int i = 0; i < width; i++)
                {
                    source[i] = samples[i % samples.Length];
                }

                source[width] = float.NaN;
                matrix[width] = float.NaN;

                // Matrix 2 and scale 0.5 leave sample magnitudes unchanged. The two tables state rounding and cutoff results independently.
                JxlQuantizer.Quantize(source.AsSpan(0, width), matrix, 0.5F, 0.5F, 1.25F, output.AsSpan(1, width));

                for (int i = 0; i < width; i++)
                {
                    int expected = i < width / 2 ? lowThreshold[i % samples.Length] : highThreshold[i % samples.Length];
                    Assert.Equal(expected, output[1 + i]);
                }

                Assert.Equal(-777, output[0]);
                Assert.Equal(-777, output[^1]);
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Checks numeric conversion, zero/unit bias, matrix scaling and correlation for both coefficient storage widths.
    /// </summary>
    [Fact]
    public void Dequantize_RestoresSignedCoefficientsAcrossStorageWidths()
    {
        static void RunTest()
        {
            ReadOnlySpan<int> pattern = [0, 1, -1, 2, -2, 4, -4];
            ReadOnlySpan<float> biases = [0.5F, 0.75F, 1.25F, 0.5F];

            // These are the independently evaluated piecewise equation and X/B correlation for the fixed input above.
            // All values and scales are binary fractions, so every path must match exactly without a tolerance.
            ReadOnlySpan<float> expectedY = [0F, 0.75F, -0.75F, 1.75F, -1.75F, 3.875F, -3.875F];
            ReadOnlySpan<float> expectedX = [0F, 0.875F, -0.875F, 2.625F, -2.625F, 5.8125F, -5.8125F];
            ReadOnlySpan<float> expectedB = [0F, 1.0625F, -1.0625F, 1.3125F, -1.3125F, 2.90625F, -2.90625F];
            ReadOnlySpan<int> lengths = [0, 1, 3, 4, 7, 8, 15, 16, 17, 31, 32, 33, 65];

            foreach (int length in lengths)
            {
                int[] integers = new int[length + 1];
                short[] shorts = new short[length + 1];
                float[] matrix = new float[(3 * length) + 1];
                float[] reconstructed = new float[length + 2];
                float[] decoded32 = new float[(3 * length) + 2];
                float[] decoded16 = new float[(3 * length) + 2];
                reconstructed.AsSpan().Fill(float.NaN);
                decoded32.AsSpan().Fill(float.NaN);
                decoded16.AsSpan().Fill(float.NaN);

                for (int i = 0; i < length; i++)
                {
                    integers[i] = pattern[i % pattern.Length];
                    shorts[i] = (short)integers[i];
                    matrix[i] = 1F;
                    matrix[length + i] = 2F;
                    matrix[(2 * length) + i] = 4F;
                }

                // Source padding must not participate. Destination guards detect stores beyond each logical output.
                integers[length] = int.MaxValue;
                shorts[length] = short.MinValue;
                matrix[3 * length] = float.NaN;
                ReadOnlySpan<int> input32 = integers.AsSpan(0, length);
                ReadOnlySpan<short> input16 = shorts.AsSpan(0, length);
                JxlQuantizer.Dequantize(input32, matrix, 1F, 1, biases, reconstructed.AsSpan(1, length));
                JxlQuantizer.Dequantize(input32, input32, input32, matrix, 1F, 0.5F, 0.25F, 0.5F, -0.25F, biases, decoded32.AsSpan(1));
                JxlQuantizer.Dequantize(input16, input16, input16, matrix, 1F, 0.5F, 0.25F, 0.5F, -0.25F, biases, decoded16.AsSpan(1));

                for (int i = 0; i < length; i++)
                {
                    int p = i % pattern.Length;
                    Assert.Equal(expectedY[p], reconstructed[1 + i]);
                    Assert.Equal(expectedX[p], decoded32[1 + i]);
                    Assert.Equal(expectedY[p], decoded32[1 + length + i]);
                    Assert.Equal(expectedB[p], decoded32[1 + (2 * length) + i]);
                }

                Assert.Equal(decoded32, decoded16);
                Assert.True(float.IsNaN(reconstructed[0]));
                Assert.True(float.IsNaN(reconstructed[^1]));
                Assert.True(float.IsNaN(decoded32[0]));
                Assert.True(float.IsNaN(decoded32[^1]));
                Assert.Equal(int.MaxValue, integers[length]);
                Assert.Equal(short.MinValue, shorts[length]);
            }

            // Encoded channel biases can be negative or negative zero. A zero integer coefficient must still reconstruct as positive zero.
            ReadOnlySpan<float> signedBiases = [-0.5F, -0F, -1F, 0.5F];
            int[] zeros = new int[33];
            float[] unitMatrix = new float[33];
            float[] zeroOutput = new float[33];
            unitMatrix.AsSpan().Fill(1F);

            for (int channel = 0; channel < 3; channel++)
            {
                JxlQuantizer.Dequantize(zeros, unitMatrix, 1F, channel, signedBiases, zeroOutput);

                foreach (float value in zeroOutput)
                {
                    Assert.Equal(0U, BitConverter.SingleToUInt32Bits(value));
                }
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Checks native default flags and the half-precision fields written for custom DC quantization.
    /// </summary>
    [Fact]
    public void QuantizationHeaders_WriteDefaultFlagsAndCustomDcValues()
    {
        using JxlDequantMatrices matrices = new();
        using MemoryStream stream = new();
        JxlBitWriter writer = new(stream);
        Assert.True(JxlQuantizerWeightsEncoder.DequantMatricesEncode(TestEnvironment.Configuration, matrices, writer, null));
        Assert.True(JxlQuantizerWeightsEncoder.DequantMatricesEncodeDc(matrices, writer));

        // Native headers use one bit equal to one for each default set, with no table fields after either flag.
        Assert.Equal(2L, writer.BitsWritten);
        writer.ZeroPadToByte();
        Assert.Equal(new byte[] { 3 }, stream.ToArray());

        stream.SetLength(0);
        stream.Position = 0;
        writer = new(stream);
        InlineArray3<float> inverseDc = default;
        inverseDc[0] = inverseDc[1] = inverseDc[2] = 1F;
        matrices.SetDcQuant(inverseDc);
        Assert.True(JxlQuantizerWeightsEncoder.DequantMatricesEncodeDc(matrices, writer));

        // Custom values use flag zero, then three half-precision fields equal to DCQuant*128.
        // Each value here is 128 (half bits 0x5800), shifted one bit by the flag in this least-significant-bit-first stream.
        Assert.Equal(49L, writer.BitsWritten);
        writer.ZeroPadToByte();
        Assert.Equal(new byte[] { 0, 0xB0, 0, 0xB0, 0, 0xB0, 0 }, stream.ToArray());
    }

    /// <summary>
    /// Checks compact table allocation, shared orientation storage, exact channel boundaries, and reuse at preparation.
    /// </summary>
    [Fact]
    public void DequantMatrices_PrepareOnlyRequestedKindsAndReuseOrientations()
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = TestEnvironment.Configuration.Clone();
        configuration.MemoryAllocator = allocator;

        using (JxlDequantMatrices matrices = new())
        {
            JxlQuantizerEncoding[] encodings = new JxlQuantizerEncoding[JxlQuantizerConstants.NumberOfQuantizerTables];
            for (int i = 0; i < encodings.Length; i++)
            {
                encodings[i] = JxlQuantizerEncoding.Library(0);
            }

            encodings[(int)JxlQuantTable.DCT8X16] = JxlQuantizerEncoding.Dct(new JxlDctQuantWeightParameters([2F, 4F, 8F], 1));
            matrices.SetEncodings(encodings);
            uint mask = (1U << (int)JxlAcStrategyType.DCT) | (1U << (int)JxlAcStrategyType.DCT8X16);
            Assert.True(matrices.EnsureComputed(configuration, mask));

            // DCT8 uses 64 coefficients per channel; the rectangle uses 128. Three channels and two directions need 1152 floats.
            // The other 15 table kinds require no coefficient storage.
            ReadOnlySpan<float> storage = matrices.GetPreparedStorage();
            Assert.Equal(1152, storage.Length);
            Assert.Equal(4608L, allocator.LiveBufferBytes);
            Assert.Equal(192, matrices.GetMatrix(storage, JxlAcStrategyType.DCT, 0).Length);
            Assert.Equal(128, matrices.GetMatrix(storage, JxlAcStrategyType.DCT, 1).Length);
            Assert.Equal(64, matrices.GetMatrix(storage, JxlAcStrategyType.DCT, 2).Length);
            Assert.Equal(384, matrices.GetMatrix(storage, JxlAcStrategyType.DCT8X16, 0).Length);

            for (int c = 0; c < 3; c++)
            {
                float weight = c == 0 ? 2F : c == 1 ? 4F : 8F;
                ReadOnlySpan<float> forward = matrices.GetMatrix(storage, JxlAcStrategyType.DCT8X16, c);
                ReadOnlySpan<float> inverse = matrices.GetInverseMatrix(storage, JxlAcStrategyType.DCT8X16, c);
                Assert.Equal(1F / weight, forward[127]);
                Assert.Equal(weight, inverse[127]);
                Assert.Equal(0F, inverse[0]);
            }

            // These two orientations use the same quantization kind. Adding the second strategy must not rent or recompute.
            mask |= 1U << (int)JxlAcStrategyType.DCT16X8;
            Assert.True(matrices.EnsureComputed(configuration, mask));
            storage = matrices.GetPreparedStorage();
            ReadOnlySpan<float> horizontal = matrices.GetMatrix(storage, JxlAcStrategyType.DCT16X8, 0);
            ReadOnlySpan<float> vertical = matrices.GetMatrix(storage, JxlAcStrategyType.DCT8X16, 0);
            Assert.True(horizontal.Overlaps(vertical, out int offset));
            Assert.Equal(0, offset);
            Assert.Single(allocator.AllocationLog);
        }

        Assert.Equal(0, allocator.LiveBufferBytes);
        Assert.Single(allocator.ReturnLog);
    }

    /// <summary>
    /// Checks that failed generation releases its allocation and permits a later complete preparation.
    /// </summary>
    [Fact]
    public void DequantMatrices_FailedPreparationReleasesReplacement()
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = TestEnvironment.Configuration.Clone();
        configuration.MemoryAllocator = allocator;

        using (JxlDequantMatrices matrices = new())
        {
            JxlQuantizerEncoding[] encodings = JxlDequantMatrices.GetLibrary();

            // Identity parameters are valid only for an 8 by 8 table. The 16 by 16 request must fail through real table generation.
            encodings[(int)JxlQuantTable.DCT16X16] = JxlQuantizerEncoding.Identity([1F, 1F, 1F, 1F, 1F, 1F, 1F, 1F, 1F]);
            matrices.SetEncodings(encodings);
            Assert.False(matrices.EnsureComputed(configuration, 1U << (int)JxlAcStrategyType.DCT16X16));
            Assert.True(matrices.GetPreparedStorage().IsEmpty);
            Assert.Equal(0, allocator.LiveBufferBytes);
            Assert.Single(allocator.ReturnLog);

            matrices.SetEncodings(JxlDequantMatrices.GetLibrary());
            Assert.True(matrices.EnsureComputed(configuration, 1U << (int)JxlAcStrategyType.DCT));
            Assert.Equal(384, matrices.GetPreparedStorage().Length);
            Assert.Equal(1536L, allocator.LiveBufferBytes);
        }

        Assert.Equal(0, allocator.LiveBufferBytes);
        Assert.Equal(2, allocator.ReturnLog.Count);
    }

    /// <summary>Checks fixed mode and DCT parameter storage through independently specified wire fields.</summary>
    [Fact]
    public void QuantizerDescriptors_ParseFixedModeAndDistanceBands()
    {
        // ID mode is 1 in three low bits. Nine half values of 1 (0x3C00) follow; the decoder multiplies each by 64.
        using (MemoryStream input = new([0x01, 0xE0, 0x01, 0xE0, 0x01, 0xE0, 0x01, 0xE0, 0x01, 0xE0, 0x01, 0xE0, 0x01, 0xE0, 0x01, 0xE0, 0x01, 0xE0, 0x01]))
        using (JxlQuantizerEncoding encoding = new())
        {
            JxlBitReader reader = new(input);
            Assert.True(JxlQuantWeights.Decode(TestEnvironment.Configuration, reader, encoding, 1, 1, 0, null));
            Assert.Equal(JxlQuantMode.Id, encoding.Mode);
            Assert.Equal(147, reader.TotalBitsConsumed);
            for (int c = 0; c < 3; c++)
            {
                for (int i = 0; i < 3; i++)
                {
                    Assert.Equal(64F, encoding.ModeWeights[c][i]);
                }
            }
        }

        // DCT mode 6 uses three bits, followed by four zero count bits (one band).
        // Half seeds 0x2800,0x2C00,0x3000 represent 1/32,1/16,1/8. Multiplication by 64 yields 2,4,8.
        using (MemoryStream input = new([0x06, 0x00, 0x14, 0x00, 0x16, 0x00, 0x18]))
        using (JxlQuantizerEncoding encoding = new())
        {
            JxlBitReader reader = new(input);
            Assert.True(JxlQuantWeights.Decode(TestEnvironment.Configuration, reader, encoding, 1, 1, 0, null));
            Assert.Equal(JxlQuantMode.Dct, encoding.Mode);
            Assert.Equal(1, encoding.DctParameters.NumDistanceBands);
            Assert.Equal(2F, encoding.DctParameters.DistanceBands[0][0]);
            Assert.Equal(4F, encoding.DctParameters.DistanceBands[1][0]);
            Assert.Equal(8F, encoding.DctParameters.DistanceBands[2][0]);
            Assert.Equal(55, reader.TotalBitsConsumed);
        }
    }

    /// <summary>Checks fixed value copies and raw ownership transfer, replacement, reuse and disposal.</summary>
    [Fact]
    public void QuantizerDescriptors_CopyFixedValuesAndTransferRawOwnership()
    {
        Span<float> packed = [2F, 4F, 8F];
        JxlDctQuantWeightParameters parameters = new(packed, 1);
        using JxlQuantizerEncoding fixedDescriptor = JxlQuantizerEncoding.Dct(in parameters);
        packed.Fill(-1F);
        parameters.DistanceBands[0][0] = -1F;
        Assert.Equal(2F, fixedDescriptor.DctParameters.DistanceBands[0][0]);

        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = TestEnvironment.Configuration.Clone();
        configuration.MemoryAllocator = allocator;
        using (JxlDequantMatrices matrices = new())
        {
            JxlQuantizerEncoding[] descriptors = JxlDequantMatrices.GetLibrary();
            JxlQuantizerEncoding raw = descriptors[(int)JxlQuantTable.DCT];
            raw.Mode = JxlQuantMode.Raw;
            raw.PrepareRawTable(configuration, 3 * 64).Fill(7);
            raw.PrepareRawTable(configuration, 3 * 64);
            Assert.Single(allocator.AllocationLog);

            // A larger real transform replaces its raw allocation once. Returning to DCT8 reuses that capacity without copying.
            raw.PrepareRawTable(configuration, 3 * 256).Fill(11);
            raw.PrepareRawTable(configuration, 3 * 64);
            Assert.Equal(2, allocator.AllocationLog.Count);
            Assert.Single(allocator.ReturnLog);
            Assert.Equal(192, raw.QuantizationTable.Length);
            matrices.SetEncodings(descriptors);
            matrices.SetEncodings(JxlDequantMatrices.GetLibrary());
            Assert.True(raw.QuantizationTable.IsEmpty);
            Assert.Equal(0, allocator.LiveBufferBytes);
        }

        Assert.Equal(2, allocator.ReturnLog.Count);
    }

    /// <summary>Checks the native raw-denominator boundary and cleanup of a failed descriptor replacement.</summary>
    /// <param name="low">The low byte of the fixed half field.</param>
    /// <param name="high">The high byte: zero or the sign and exponent bits for minus one.</param>
    [Theory]
    [InlineData((byte)0, (byte)0)]
    [InlineData((byte)0, (byte)0xBC)]
    public void RawQuantizer_InvalidDenominatorReleasesOwnedSamples(byte low, byte high)
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = TestEnvironment.Configuration.Clone();
        configuration.MemoryAllocator = allocator;
        using JxlQuantizerEncoding encoding = new() { Mode = JxlQuantMode.Raw };
        encoding.PrepareRawTable(configuration, 3 * 64).Fill(1);
        using MemoryStream input = new([low, high]);
        JxlBitReader reader = new(input);
        Assert.Throws<InvalidImageContentException>(
            () => JxlModularFrameDecoder.DecodeQuantizerTable(configuration, 8, 8, reader, encoding, 0, null));

        Assert.True(encoding.QuantizationTable.IsEmpty);
        Assert.Equal(0, allocator.LiveBufferBytes);
        Assert.Single(allocator.ReturnLog);
    }

    /// <summary>Checks both complete matrix-header forms, descriptor reuse, and retained coefficient allocation capacity.</summary>
    /// <param name="allDefault">Whether the wire uses its one-bit shortcut instead of seventeen Library modes.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DequantMatrices_ReadHeadersReusesSlotsAndInvalidatesPreparedViews(bool allDefault)
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = TestEnvironment.Configuration.Clone();
        configuration.MemoryAllocator = allocator;
        using (JxlDequantMatrices matrices = new())
        {
            JxlQuantizerEncoding first = matrices.GetEncodings()[0];
            JxlQuantizerEncoding last = matrices.GetEncodings()[^1];
            uint strategy = 1U << (int)JxlAcStrategyType.DCT;
            Assert.True(matrices.EnsureComputed(configuration, strategy));
            long preparedBytes = allocator.LiveBufferBytes;
            last.Mode = JxlQuantMode.Raw;
            last.PrepareRawTable(configuration, 192).Fill(1);

            // Flag one has no payload. Flag zero requires seventeen three-bit Library modes (all zero); the library index has zero bits.
            using MemoryStream input = new(allDefault ? [1] : [0, 0, 0, 0, 0, 0, 0]);
            JxlBitReader reader = new(input);
            Assert.True(matrices.Decode(configuration, reader, null));
            Assert.Equal(allDefault ? 1L : 52L, reader.TotalBitsConsumed);
            Assert.Same(first, matrices.GetEncodings()[0]);
            Assert.Same(last, matrices.GetEncodings()[^1]);
            Assert.True(last.QuantizationTable.IsEmpty);
            Assert.True(matrices.GetPreparedStorage().IsEmpty);
            Assert.Equal(preparedBytes, allocator.LiveBufferBytes);
            foreach (JxlQuantizerEncoding descriptor in matrices.GetEncodings())
            {
                Assert.Equal(JxlQuantMode.Library, descriptor.Mode);
                Assert.Equal(0, descriptor.Predefined);
            }

            // Parsing invalidates coefficient contents, but preparation can regenerate into the same allocation after borrowers have finished.
            Assert.True(matrices.EnsureComputed(configuration, strategy));
            Assert.Equal(2, allocator.AllocationLog.Count);
            Assert.Equal(preparedBytes, allocator.LiveBufferBytes);
        }

        Assert.Equal(0, allocator.LiveBufferBytes);
        Assert.Equal(2, allocator.ReturnLog.Count);
    }

    /// <summary>Checks cleanup after a later invalid descriptor and after truncated custom matrix fields.</summary>
    /// <param name="truncated">Whether the first DCT descriptor ends before its second channel seed.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DequantMatrices_FailedHeaderReleasesRawPayloadsAndResetsSlots(bool truncated)
    {
        TestMemoryAllocator allocator = new();
        allocator.EnableNonThreadSafeLogging();
        Configuration configuration = TestEnvironment.Configuration.Clone();
        configuration.MemoryAllocator = allocator;
        using JxlDequantMatrices matrices = new();
        JxlQuantizerEncoding last = matrices.GetEncodings()[^1];
        last.Mode = JxlQuantMode.Raw;
        last.PrepareRawTable(configuration, 192).Fill(1);

        // Flag zero, DCT mode 6, one band, then half seeds 1/32,1/16,1/8 occupy exactly seven bytes.
        // The next descriptor is ID mode 1 followed by a forbidden half NaN (0x7E00). No fields come from the library writer.
        using MemoryStream input = new(truncated
            ? [0x0C, 0x00, 0x28]
            : [0x0C, 0x00, 0x28, 0x00, 0x2C, 0x00, 0x30, 0x01, 0xF0, 0x03]);

        JxlBitReader reader = new(input);
        if (truncated)
        {
            Assert.Throws<EndOfStreamException>(() => matrices.Decode(configuration, reader, null));
        }
        else
        {
            Assert.False(matrices.Decode(configuration, reader, null));
        }

        Assert.Same(last, matrices.GetEncodings()[^1]);
        Assert.True(last.QuantizationTable.IsEmpty);
        Assert.True(matrices.GetPreparedStorage().IsEmpty);
        foreach (JxlQuantizerEncoding descriptor in matrices.GetEncodings())
        {
            Assert.Equal(JxlQuantMode.Library, descriptor.Mode);
        }

        Assert.Equal(0, allocator.LiveBufferBytes);
        Assert.Single(allocator.ReturnLog);
    }

    /// <summary>Checks DC wire scaling, reciprocal publication, and explicit defaults on a reused owner.</summary>
    [Fact]
    public void DequantMatrices_ReadDcScalesFieldsAndRestoresDefaults()
    {
        using JxlDequantMatrices matrices = new();

        // Flag zero precedes half fields 128,256,512 (0x5800,0x5C00,0x6000). The decoded DC multipliers are 1,2,4.
        using (MemoryStream input = new([0x00, 0xB0, 0x00, 0xB8, 0x00, 0xC0, 0x00]))
        {
            JxlBitReader reader = new(input);
            Assert.True(matrices.DecodeDc(reader));
            Assert.Equal(49L, reader.TotalBitsConsumed);
            for (int c = 0; c < 3; c++)
            {
                Assert.Equal((float)(1 << c), matrices.GetDcQuant(c));
                Assert.Equal(1F / (1 << c), matrices.GetInverseDcQuant(c));
            }
        }

        using MemoryStream defaults = new([1]);
        Assert.True(matrices.DecodeDc(new(defaults)));
        for (int c = 0; c < 3; c++)
        {
            Assert.Equal(JxlQuantizerConstants.DcQuant[c], matrices.GetDcQuant(c));
            Assert.Equal(JxlQuantizerConstants.InverseDcQuant[c], matrices.GetInverseDcQuant(c));
        }
    }

    /// <summary>Checks that invalid later DC fields cannot publish an earlier valid channel.</summary>
    /// <param name="byte2">The byte that contains the second half field's low seven bits.</param>
    /// <param name="byte3">The byte that contains the middle eight bits of that shifted field.</param>
    /// <param name="byte4">The byte that contains the field's remaining sign bit.</param>
    [Theory]
    [InlineData((byte)0, (byte)0, (byte)0)]
    [InlineData((byte)0, (byte)0x78, (byte)1)]
    [InlineData((byte)0, (byte)0xFC, (byte)0)]
    [InlineData((byte)2, (byte)0, (byte)0)]
    public void DequantMatrices_InvalidDcFieldsLeaveAllPreviousValues(byte byte2, byte byte3, byte byte4)
    {
        using JxlDequantMatrices matrices = new();

        // First channel is a valid half 128. Second is zero, minus one, NaN, or the smallest subnormal; F16/128 is too small for the last case.
        using MemoryStream input = new([0x00, 0xB0, byte2, byte3, byte4]);
        Assert.False(matrices.DecodeDc(new(input)));
        for (int c = 0; c < 3; c++)
        {
            Assert.Equal(JxlQuantizerConstants.DcQuant[c], matrices.GetDcQuant(c));
            Assert.Equal(JxlQuantizerConstants.InverseDcQuant[c], matrices.GetInverseDcQuant(c));
        }
    }

    /// <summary>Checks the existing reader's EOF boundary after a valid first DC field.</summary>
    [Fact]
    public void DequantMatrices_TruncatedDcHeaderLeavesAllPreviousValues()
    {
        using JxlDequantMatrices matrices = new();
        using MemoryStream input = new([0x00, 0xB0, 0x00]);
        Assert.Throws<EndOfStreamException>(() => matrices.DecodeDc(new(input)));
        for (int c = 0; c < 3; c++)
        {
            Assert.Equal(JxlQuantizerConstants.DcQuant[c], matrices.GetDcQuant(c));
            Assert.Equal(JxlQuantizerConstants.InverseDcQuant[c], matrices.GetInverseDcQuant(c));
        }
    }
}
