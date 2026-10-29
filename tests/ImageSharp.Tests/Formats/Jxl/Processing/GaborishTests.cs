// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing;

public class GaborishTests
{
    /// <summary>
    /// Checks stage row origins, declared halos, channel weights and exact output bounds at every SIMD capability.
    /// </summary>
    /// <param name="width">The center range width.</param>
    /// <param name="left">The requested samples before the center range.</param>
    /// <param name="right">The requested samples after the center range.</param>
    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(3, 1, 2)]
    [InlineData(17, 7, 3)]
    [InlineData(33, 2, 1)]
    [InlineData(33, 31, 0)]
    public void Stage_UsesExplicitHaloAndPreservesUnrequestedSamples(int width, int left, int right)
    {
        static void RunTest(string serialized)
        {
            (int sampleWidth, int extraLeft, int extraRight) = FeatureTestRunner.Deserialize<(int, int, int)>(serialized);
            const int origin = RenderPipelineStageBase.RenderPipelineXOffset;
            const float untouched = -123F;
            int outputLength = extraLeft + sampleWidth + extraRight;
            int storageWidth = origin + sampleWidth + extraRight + 2;
            Configuration configuration = TestEnvironment.Configuration;
            using JxlPlane<float> inputStorage = new(configuration, storageWidth, 9);
            using JxlPlane<float> outputStorage = new(configuration, storageWidth, 3);
            Span<float> inputSamples = inputStorage.GetRow(0);
            Span<float> outputSamples = outputStorage.GetRow(0);
            inputSamples.Fill(untouched);
            outputSamples.Fill(untouched);
            Memory<float>[] inputTable = new Memory<float>[9];
            Memory<float>[] outputTable = new Memory<float>[3];

            // The table is managed metadata containing borrowed memories. Numeric samples remain in the allocator-owned planes.
            // Three table columns select channels; its three rows select the upper, current and lower source row.
            for (int row = 0; row < 3; row++)
            {
                for (int c = 0; c < 3; c++)
                {
                    int storageRow = (row * 3) + c;
                    inputTable[storageRow] = inputStorage.GetRowMemory(storageRow)[..storageWidth];
                    Span<float> samples = inputTable[storageRow].Span;
                    for (int x = -extraLeft - 1; x <= sampleWidth + extraRight; x++)
                    {
                        // Nonlinear integer values distinguish axial and diagonal weights without rounding ambiguity.
                        samples[origin + x] = ((c + 1) * 32) + (row * row * 8) + (x * x) + ((row + 1) * (x % 3));
                    }
                }
            }

            for (int c = 0; c < 3; c++)
            {
                outputTable[c] = outputStorage.GetRowMemory(c)[..storageWidth];
            }

            using Buffer2D<Memory<float>> inputRows = new(MemoryGroup<Memory<float>>.Wrap(inputTable.AsMemory()), 3, 3);
            using Buffer2D<Memory<float>> outputRows = new(MemoryGroup<Memory<float>>.Wrap(outputTable.AsMemory()), 3, 1);
            JxlLoopFilter filter = new()
            {
                GaborishXWeight1 = 0.25F,
                GaborishXWeight2 = 0.5F,
                GaborishYWeight1 = 0.5F,
                GaborishYWeight2 = 0.25F,
                GaborishBWeight1 = 0F,
                GaborishBWeight2 = 0.75F
            };

            using GaborishStage stage = new(configuration, filter);
            stage.ProcessRow(inputRows, outputRows, extraLeft, extraRight, sampleWidth, 0, 0);

            // All three kernels have raw sum4, so normalization gives exact binary fractions in center/axial/diagonal order.
            ReadOnlySpan<float> expectedWeights = [0.25F, 0.0625F, 0.125F, 0.25F, 0.125F, 0.0625F, 0.25F, 0F, 0.1875F];
            for (int c = 0; c < 3; c++)
            {
                Assert.Equal(RenderPipelineChannelMode.InOut, stage.GetChannelMode(c));
                ReadOnlySpan<float> top = inputTable[c].Span;
                ReadOnlySpan<float> middle = inputTable[3 + c].Span;
                ReadOnlySpan<float> bottom = inputTable[6 + c].Span;
                ReadOnlySpan<float> actual = outputSamples.Slice(c * outputStorage.PixelsPerRow, outputStorage.PixelsPerRow);
                int begin = origin - extraLeft;
                int end = begin + outputLength;
                for (int x = 0; x < actual.Length; x++)
                {
                    if (x < begin || x >= end)
                    {
                        Assert.Equal(untouched, actual[x]);
                        continue;
                    }

                    float axial = (middle[x - 1] + middle[x + 1]) + (top[x] + bottom[x]);
                    float diagonal = (top[x - 1] + top[x + 1]) + (bottom[x - 1] + bottom[x + 1]);
                    float expected = (middle[x] * expectedWeights[c * 3])
                        + (axial * expectedWeights[(c * 3) + 1])
                        + (diagonal * expectedWeights[(c * 3) + 2]);
                    Assert.Equal(expected, actual[x]);
                }
            }

            Assert.Equal(RenderPipelineChannelMode.Ignored, stage.GetChannelMode(3));
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            (width, left, right),
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Checks all six five-by-five tap weights, full-image reflection and distinct rectangle origins at every SIMD capability.
    /// </summary>
    /// <param name="width">The requested output width, including short rows and SIMD tails.</param>
    /// <param name="cropped">Whether the source rectangle has readable neighbors outside its requested range.</param>
    [Theory]
    [InlineData(1, false)]
    [InlineData(3, false)]
    [InlineData(17, false)]
    [InlineData(33, false)]
    [InlineData(17, true)]
    public void Symmetric5_UsesDistinctTapWeightsAndFullImageBorders(int width, bool cropped)
    {
        static void RunTest(string serialized)
        {
            (int sampleWidth, bool useCrop) = FeatureTestRunner.Deserialize<(int, bool)>(serialized);
            const float padding = -123F;
            const int height = 3;
            int inputWidth = useCrop ? sampleWidth + 6 : sampleWidth;
            int inputHeight = useCrop ? 7 : height;
            Rectangle inputRect = new(useCrop ? 3 : 0, useCrop ? 2 : 0, sampleWidth, height);
            Rectangle outputRect = new(2, 1, sampleWidth, height);
            using JxlPlane<float> input = new(TestEnvironment.Configuration, inputWidth, inputHeight);
            using JxlPlane<float> output = new(TestEnvironment.Configuration, sampleWidth + 5, height + 3);
            Span<float> inputSamples = input.GetRow(0);
            Span<float> outputSamples = output.GetRow(0);
            inputSamples.Fill(padding);
            outputSamples.Fill(padding);

            for (int y = 0; y < inputHeight; y++)
            {
                for (int x = 0; x < inputWidth; x++)
                {
                    inputSamples[(y * input.PixelsPerRow) + x] = ((13 * x) + (7 * y)) % 31 - 15;
                }
            }

            // Six different dyadic weights distinguish the center, axial, diagonal, outer axial, L and corner taps.
            // Every product and sum remains exactly representable for these small integer samples.
            JxlWeightsSymmetric5 weights = new()
            {
                C = JxlWeightsSymmetric5.CreateVector4(0.5F),
                R = JxlWeightsSymmetric5.CreateVector4(0.0625F),
                R2 = JxlWeightsSymmetric5.CreateVector4(0.03125F),
                D = JxlWeightsSymmetric5.CreateVector4(0.015625F),
                L = JxlWeightsSymmetric5.CreateVector4(0.00390625F),
                D2 = JxlWeightsSymmetric5.CreateVector4(0.0078125F)
            };

            Assert.True(JxlConvolve.Symmetric5(input, inputRect, ref weights, output, outputRect));
            ReadOnlySpan<float> weightBySquaredDistance = [0.5F, 0.0625F, 0.015625F, 0F, 0.03125F, 0.00390625F, 0F, 0F, 0.0078125F];

            for (int y = 0; y < output.YSize; y++)
            {
                for (int x = 0; x < output.PixelsPerRow; x++)
                {
                    float actual = outputSamples[(y * output.PixelsPerRow) + x];
                    if (!outputRect.Contains(x, y))
                    {
                        Assert.Equal(padding, actual);
                        continue;
                    }

                    float expected = 0;
                    for (int dy = -2; dy <= 2; dy++)
                    {
                        int sourceY = inputRect.Y + y - outputRect.Y + dy;

                        // The edge sample repeats. Repeated reflection also defines a one-sample image.
                        while ((uint)sourceY >= (uint)inputHeight)
                        {
                            sourceY = sourceY < 0 ? -sourceY - 1 : (2 * inputHeight) - sourceY - 1;
                        }

                        for (int dx = -2; dx <= 2; dx++)
                        {
                            int sourceX = inputRect.X + x - outputRect.X + dx;
                            while ((uint)sourceX >= (uint)inputWidth)
                            {
                                sourceX = sourceX < 0 ? -sourceX - 1 : (2 * inputWidth) - sourceX - 1;
                            }

                            float sample = ((13 * sourceX) + (7 * sourceY)) % 31 - 15;
                            expected += sample * weightBySquaredDistance[(dx * dx) + (dy * dy)];
                        }
                    }

                    Assert.Equal(expected, actual);
                }
            }

            // The convolution borrows the source. It must preserve its samples and allocator padding.
            for (int y = 0; y < inputHeight; y++)
            {
                for (int x = 0; x < input.PixelsPerRow; x++)
                {
                    float expected = x < inputWidth ? ((13 * x) + (7 * y)) % 31 - 15 : padding;
                    Assert.Equal(expected, inputSamples[(y * input.PixelsPerRow) + x]);
                }
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            (width, cropped),
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    private static JxlWeightsSymmetric3 GaborishKernel(float weight1, float weight2)
    {
        const float weight0 = 1.0f;

        // Normalize.
        float mul = 1.0f / (weight0 + (4.0f * (weight1 + weight2)));
        float w0 = weight0 * mul;
        float w1 = weight1 * mul;
        float w2 = weight2 * mul;

        JxlWeightsSymmetric3 wgt = new();
        wgt.SetC(Vector128.Create(w0));
        wgt.SetD(Vector128.Create(w1));
        wgt.SetR(Vector128.Create(w2));

        return wgt;
    }

    private static void ConvolveGaborish(
        JxlPlane<float> input,
        float weight1,
        float weight2,
        JxlPlane<float> output)
    {
        Assert.True(JxlImageOperations.SameSize(input, output));

        // The test will fail if this throws.
        JxlConvolve.SlowSymmetric3(
            input,
            input.GetRectangle(),
            GaborishKernel(weight1, weight2),
            output);
    }

    private static void TestRoundTrip(
        JxlImage3<float> input,
        float maxL1)
    {
        JxlImage3<float> fwd = new(TestEnvironment.Configuration, input.XSize, input.YSize);

        ConvolveGaborish(
            input.Plane(0),
            0,
            0,
            fwd.Plane(0));

        ConvolveGaborish(
            input.Plane(1),
            0,
            0,
            fwd.Plane(1));

        ConvolveGaborish(
            input.Plane(2),
            0,
            0,
            fwd.Plane(2));

        const float w = 0.92718927264540152f;

        InlineArray3<float> weights = default;
        weights[0] = w;
        weights[1] = w;
        weights[2] = w;

        // The test will fail if this throws.
        JxlGaborish.InverseGaborish(
            TestEnvironment.Configuration,
            fwd,
            fwd.GetRectangle(),
            weights);

        // TODO: VerifyRelativeError
        Assert.True(
            VerifyRelativeError(
                input,
                fwd,
                maxL1,
                1e-4f));
    }

    [Fact]
    public void TestZero()
    {
        JxlImage3<float> input = new(TestEnvironment.Configuration, 20, 20);
        JxlImageOperations.ZeroFillImage(input);
        TestRoundTrip(input, 0.0f);
    }

    [Fact]
    public void TestFlat()
    {
        JxlImage3<float> input = new(TestEnvironment.Configuration, 20, 20);
        JxlImageOperations.FillImage(1.0f, input);
        TestRoundTrip(input, 1e-5f);
    }
}
