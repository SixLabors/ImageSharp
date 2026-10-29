// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Quantization;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing;

public class QuantizationTests
{
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
