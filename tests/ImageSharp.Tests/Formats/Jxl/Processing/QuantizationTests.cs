// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing;

public class QuantizationTests
{
    /// <summary>
    /// Checks the native default band count and the rectangular transform's predefined table selection.
    /// </summary>
    [Fact]
    public void DefaultWeights_MatchNativeBandAndTableMetadata()
    {
        Assert.Equal(8, JxlQuantWeights.Dct32x32.DctParameters!.NumDistanceBands);
        Assert.Same(JxlQuantWeights.Dct32x64, JxlDequantMatrices.Library[(int)JxlQuantTable.DCT32X64]);
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
            float[][] constantBands = [[2F, 0F, 0F], [4F, 0F, 0F], [8F, 0F, 0F]];
            float[][] varyingBands = [[2F, 1F, -1F], [4F, 1F, -1F], [8F, 1F, -1F]];
            ReadOnlySpan<int> columns = [3, 4, 7, 8, 15, 16, 17, 31, 32, 33];

            foreach (int columnCount in columns)
            {
                const int rowCount = 3;
                int channelSize = rowCount * columnCount;
                float[] storage = new float[(3 * channelSize) + 2];
                storage.AsSpan().Fill(-77F);
                Span<float> weights = storage.AsSpan(1, 3 * channelSize);
                Assert.True(JxlQuantWeights.GetQuantWeights(rowCount, columnCount, constantBands, 3, weights));

                for (int c = 0; c < 3; c++)
                {
                    for (int i = 0; i < channelSize; i++)
                    {
                        Assert.Equal(constantBands[c][0], weights[(c * channelSize) + i]);
                    }
                }

                Assert.Equal(-77F, storage[0]);
                Assert.Equal(-77F, storage[^1]);

                // Mult(1)=2 and Mult(-1)=1/2 produce positive cumulative bands [a,2a,a]. Raw multiplication would reject them.
                Assert.True(JxlQuantWeights.GetQuantWeights(rowCount, columnCount, varyingBands, 3, weights));
                for (int c = 0; c < 3; c++)
                {
                    Assert.Equal(varyingBands[c][0], weights[c * channelSize]);
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
}
