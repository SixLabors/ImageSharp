// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.AcStrategy;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Dct;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing;

/// <summary>
/// Tests exact DCT basis cases, coefficient orientation and the small transform contracts used by JPEG XL.
/// </summary>
public class DctTests
{
    /// <summary>
    /// Checks one-point identity and the independent two-point sum/difference equations without allowing trailing-span copies.
    /// </summary>
    [Fact]
    public void SmallDct_PreservesIdentityAndDistinctPairOutputs()
    {
        static void RunTest()
        {
            float[] workspace = new float[16];
            float[] single = [6F, 123F];
            float[] coefficients = [123F, 123F, 123F];
            float[] pixels = [123F, 123F, 123F];
            JxlDct.ComputeScaledDct(1, 1, single, 1, coefficients.AsSpan(1), workspace);
            Assert.Equal(6F, coefficients[1]);
            Assert.Equal(123F, coefficients[0]);
            Assert.Equal(123F, coefficients[2]);
            JxlDct.ComputeScaledInverseDct(1, 1, coefficients.AsSpan(1), pixels.AsSpan(1), 1, workspace);
            Assert.Equal(6F, pixels[1]);
            Assert.Equal(123F, pixels[0]);
            Assert.Equal(123F, pixels[2]);

            ReadOnlySpan<(int Rows, int Columns)> shapes = [(1, 2), (2, 1)];
            foreach ((int rows, int columns) in shapes)
            {
                float[] pair = [6F, 2F];
                coefficients.AsSpan().Fill(123F);
                JxlDct.ComputeScaledDct(rows, columns, pair, columns, coefficients, workspace);
                Assert.Equal(4F, coefficients[0]);
                Assert.Equal(2F, coefficients[1]);
                Assert.Equal(123F, coefficients[2]);

                // Supply independent frequency values. A paired forward/inverse defect must not be able to cancel itself.
                coefficients[0] = 4F;
                coefficients[1] = 2F;
                pixels.AsSpan().Fill(123F);
                JxlDct.ComputeScaledInverseDct(rows, columns, coefficients, pixels, columns, workspace);
                Assert.Equal(6F, pixels[0]);
                Assert.Equal(2F, pixels[1]);
                Assert.Equal(123F, pixels[2]);
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Checks an exact non-DC cosine basis at every supported length, both axis orientations, real strides and SIMD/scalar dispatch.
    /// </summary>
    [Fact]
    public void Dct_HalfFrequencyBasisPreservesAxesAndBounds()
    {
        static void RunTest()
        {
            ReadOnlySpan<int> lengths = [2, 4, 8, 16, 32, 64, 128, 256];
            ReadOnlySpan<bool> orientations = [false, true];
            const int prefix = 3;
            const float sentinel = 123F;
            foreach (int length in lengths)
            {
                foreach (bool horizontal in orientations)
                {
                    int rows = horizontal ? 32 : length;
                    int columns = horizontal ? length : 32;
                    int sourceStride = columns + 5;
                    int destinationStride = columns + 7;
                    int area = rows * columns;
                    float[] source = new float[prefix + (rows * sourceStride) + 3];
                    float[] coefficients = new float[prefix + area + 3];
                    float[] destination = new float[prefix + (rows * destinationStride) + 3];
                    source.AsSpan().Fill(sentinel);
                    coefficients.AsSpan().Fill(sentinel);
                    destination.AsSpan().Fill(sentinel);

                    // sqrt(2)*cos((sample+1/2)*pi/2) is exactly the repeating sign pattern [1,-1,-1,1].
                    // Its normalized DCT has one unit coefficient at frequency length/2, with the other axis at DC.
                    for (int y = 0; y < rows; y++)
                    {
                        for (int x = 0; x < columns; x++)
                        {
                            int position = (horizontal ? x : y) & 3;
                            source[prefix + (y * sourceStride) + x] = position is 0 or 3 ? 1F : -1F;
                        }
                    }

                    // The narrow axis caps row-band lanes. Include the existing block region and two banks, but no hidden padding allowance.
                    int lanes = Math.Min(16, Math.Min(rows, columns));
                    int workspaceLength = area + (2 * Math.Max(rows, columns) * lanes);
                    float[] workspace = new float[workspaceLength + 2];
                    workspace.AsSpan().Fill(sentinel);
                    Span<float> work = workspace.AsSpan(1, workspaceLength);
                    Span<float> coefficientBlock = coefficients.AsSpan(prefix);
                    JxlDct.ComputeScaledDct(rows, columns, source.AsSpan(prefix), sourceStride, coefficientBlock, work);
                    int frequencyY = horizontal ? 0 : length / 2;
                    int frequencyX = horizontal ? length / 2 : 0;
                    int index = rows >= columns ? (frequencyX * rows) + frequencyY : (frequencyY * columns) + frequencyX;
                    Assert.Equal(1F, coefficientBlock[index]);
                    coefficientBlock[index] = 0;
                    Assert.True(coefficientBlock[..area].IndexOfAnyExcept(0F) < 0);
                    Assert.True(coefficients.AsSpan(0, prefix).IndexOfAnyExcept(sentinel) < 0);
                    Assert.True(coefficients.AsSpan(prefix + area).IndexOfAnyExcept(sentinel) < 0);

                    coefficientBlock[..area].Clear();
                    coefficientBlock[index] = 1F;
                    JxlDct.ComputeScaledInverseDct(rows, columns, coefficientBlock, destination.AsSpan(prefix), destinationStride, work);
                    for (int i = 0; i < destination.Length; i++)
                    {
                        int relative = i - prefix;
                        int y = relative / destinationStride;
                        int x = relative % destinationStride;
                        int position = (horizontal ? x : y) & 3;
                        float expected = relative >= 0 && y < rows && x < columns ? position is 0 or 3 ? 1F : -1F : sentinel;
                        Assert.Equal(expected, destination[i]);
                    }

                    // Forward conversion must leave its source and padding unchanged, even when later row-band stores reuse the output matrix.
                    for (int i = 0; i < source.Length; i++)
                    {
                        int relative = i - prefix;
                        int y = relative / sourceStride;
                        int x = relative % sourceStride;
                        int position = (horizontal ? x : y) & 3;
                        float expected = relative >= 0 && y < rows && x < columns ? position is 0 or 3 ? 1F : -1F : sentinel;
                        Assert.Equal(expected, source[i]);
                    }

                    Assert.Equal(sentinel, workspace[0]);
                    Assert.Equal(sentinel, workspace[^1]);
                }
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Checks a nonzero odd-frequency basis independently in each direction, including the signed weighted combination.
    /// </summary>
    [Fact]
    public void Dct_OddFrequencyUsesBothWeightedHalves()
    {
        static void RunTest()
        {
            ReadOnlySpan<bool> orientations = [false, true];
            ReadOnlySpan<float> samples = [0F, 1F, -1F, 0F];

            // These are the four samples of sqrt(2)*cos(3*pi*(sample+1/2)/4), rounded once to float.
            ReadOnlySpan<float> basis = [0.541196100146197F, -1.3065629648763764F, 1.3065629648763764F, -0.541196100146197F];
            foreach (bool horizontal in orientations)
            {
                int rows = horizontal ? 32 : 4;
                int columns = horizontal ? 4 : 32;
                int stride = columns + 3;
                float[] source = new float[rows * stride];
                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < columns; x++)
                    {
                        source[(y * stride) + x] = samples[horizontal ? x : y];
                    }
                }

                float[] coefficients = new float[rows * columns];
                float[] workspace = new float[512];
                JxlDct.ComputeScaledDct(rows, columns, source, stride, coefficients, workspace);

                // Frequency three of [0,1,-1,0] is -sqrt(2)*cos(pi/8)/2. The other axis is DC.
                Assert.Equal(-0.6532814824381882F, coefficients[3 * 32]);
                coefficients.AsSpan().Clear();
                coefficients[3 * 32] = 1F;
                float[] restored = new float[rows * stride];
                JxlDct.ComputeScaledInverseDct(rows, columns, coefficients, restored, stride, workspace);
                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < columns; x++)
                    {
                        Assert.Equal(basis[horizontal ? x : y], restored[(y * stride) + x]);
                    }
                }
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Checks separate horizontal and vertical resampling factors with an exact two-point frequency basis.
    /// </summary>
    [Fact]
    public void ReinterpretingDct_UsesIndependentFrequencyAxes()
    {
        static void RunTest()
        {
            float[] source = [1F, -1F, 123F, 1F, -1F, 123F, 1F, -1F, 123F, 1F, -1F, 123F];
            float[] output = new float[14];
            output.AsSpan().Fill(123F);
            float[] coefficients = new float[8];
            float[] workspace = new float[1056];
            JxlTransformsDecoder.ReinterpretingDct(32, 16, 4, 2, 4, 2, source, 3, output, 7, coefficients, workspace);

            // The nonzero frequency is on the two-sample axis. The four-sample axis is DC, whose scale is exactly one.
            Assert.Equal(1.108937353592731823F, output[7]);
            Assert.Equal(123F, output[4]);
            Assert.Equal(123F, output[11]);

            output.AsSpan().Clear();
            output[7] = 1;
            float[] restored = new float[12];
            restored.AsSpan().Fill(123F);
            JxlTransformsEncoder.ReinterpretingInverseDct(32, 16, 4, 2, 4, 2, output, 7, restored, 3, workspace);
            for (int y = 0; y < 4; y++)
            {
                Assert.Equal(0.901764195028874394F, restored[y * 3]);
                Assert.Equal(-0.901764195028874394F, restored[(y * 3) + 1]);
                Assert.Equal(123F, restored[(y * 3) + 2]);
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Checks independent Haar equations and the complete AFV component placement without floating-point tolerances.
    /// </summary>
    [Fact]
    public void SmallTransforms_PreserveHaarSignsAndAfvDc()
    {
        static void RunTest()
        {
            float[] source = new float[88];
            source.AsSpan().Fill(123F);
            source[0] = 1;
            source[1] = 2;
            source[11] = 3;
            source[12] = 4;
            float[] coefficients = new float[64];
            JxlTransformsEncoder.Dct2TopBlock(2, source, 11, coefficients);
            Assert.Equal(2.5F, coefficients[0]);
            Assert.Equal(-0.5F, coefficients[1]);
            Assert.Equal(-1F, coefficients[8]);
            Assert.Equal(0F, coefficients[9]);

            coefficients.AsSpan().Clear();
            coefficients[0] = 2.5F;
            coefficients[1] = -0.5F;
            coefficients[8] = -1F;
            float[] restored = new float[88];
            restored.AsSpan().Fill(123F);
            JxlTransformsDecoder.Idct2TopBlock(2, coefficients, 11, restored);
            Assert.Equal(1F, restored[0]);
            Assert.Equal(2F, restored[1]);
            Assert.Equal(3F, restored[11]);
            Assert.Equal(4F, restored[12]);
            Assert.Equal(123F, restored[2]);
            Assert.Equal(123F, restored[13]);

            // Repeated [1,2;3,4] cells have fixed first-stage Haar quadrants. Later stages only reduce their constant average quadrant.
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    source[(y * 11) + x] = 1 + (x & 1) + (2 * (y & 1));
                }
            }

            float[] haarWorkspace = new float[256];
            JxlTransformsEncoder.TransformFromPixels(JxlAcStrategyType.DCT2X2, source, 11, coefficients, haarWorkspace);
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    float expected = x == 0 && y == 0 ? 2.5F : x >= 4 && y < 4 ? -0.5F : x < 4 && y >= 4 ? -1F : 0F;
                    Assert.Equal(expected, coefficients[(y * 8) + x]);
                }
            }

            restored.AsSpan().Fill(123F);
            JxlTransformsDecoder.TransformToPixels(JxlAcStrategyType.DCT2X2, coefficients, restored, 11, haarWorkspace);
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    Assert.Equal(1F + (x & 1) + (2 * (y & 1)), restored[(y * 11) + x]);
                }

                Assert.True(restored.AsSpan((y * 11) + 8, 3).IndexOfAnyExcept(123F) < 0);
            }

            ReadOnlySpan<JxlAcStrategyType> strategies =
            [
                JxlAcStrategyType.AFV0, JxlAcStrategyType.AFV1, JxlAcStrategyType.AFV2, JxlAcStrategyType.AFV3
            ];

            foreach (JxlAcStrategyType strategy in strategies)
            {
                for (int y = 0; y < 8; y++)
                {
                    source.AsSpan(y * 11, 8).Fill(1F);
                }

                coefficients.AsSpan().Fill(123F);
                float[] workspace = new float[256];
                JxlTransformsEncoder.TransformFromPixels(strategy, source, 11, coefficients, workspace);
                Assert.Equal(1F, coefficients[0]);
                Assert.Equal(1F, source[0]);
                Assert.Equal(1F, source[7]);

                // A unit DC coefficient independently restores a constant block in all three AFV regions and each quadrant orientation.
                coefficients.AsSpan().Clear();
                coefficients[0] = 1;
                restored.AsSpan().Fill(123F);
                JxlTransformsDecoder.TransformToPixels(strategy, coefficients, restored, 11, workspace);
                for (int y = 0; y < 8; y++)
                {
                    Assert.True(restored.AsSpan(y * 11, 8).IndexOfAnyExcept(1F) < 0);
                    Assert.True(restored.AsSpan((y * 11) + 8, 3).IndexOfAnyExcept(123F) < 0);
                }
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }
}
