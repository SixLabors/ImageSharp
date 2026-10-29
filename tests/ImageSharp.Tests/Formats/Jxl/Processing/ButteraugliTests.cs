// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using Microsoft.Diagnostics.Runtime.Interop;
using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Butteraugli;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Jxl.Processing;

/// <summary>
/// Tests for Google Butteraugli (C# implementation), a tool
/// used for comparing images in a more sophisticated way
/// (the way humans may notice differences). For example,
/// if every pixel was off by 1, PSNR, which operates on a
/// pixel-by-pixel basis, would report a large difference,
/// while Butteraugli would report barely any differences,
/// as us humans wouldn't notice anything if every pixel
/// was just off by one.
/// </summary>
public class ButteraugliTests
{
    /// <summary>
    /// Checks weighted squared differences, accumulation and reused output across all SIMD widths and scalar tails.
    /// </summary>
    /// <param name="width">The logical width, independent of the padded physical stride.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(17)]
    [InlineData(33)]
    public void L2Differences_PreserveWeightsAndAccumulation(int width)
    {
        // The feature runner invokes this same numerical contract in a process with each selected SIMD capability.
        static void RunTest(string serialized)
        {
            int sampleWidth = FeatureTestRunner.Deserialize<int>(serialized);
            const int height = 3;
            const float padding = -123F;
            using JxlPlane<float> reference = new(TestEnvironment.Configuration, sampleWidth, height);
            using JxlPlane<float> distorted = new(TestEnvironment.Configuration, sampleWidth, height);
            using JxlPlane<float> difference = new(TestEnvironment.Configuration, sampleWidth, height);
            Span<float> referenceSamples = reference.GetRow(0);
            Span<float> distortedSamples = distorted.GetRow(0);
            Span<float> differenceSamples = difference.GetRow(0);
            differenceSamples.Fill(padding);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < sampleWidth; x++)
                {
                    float value = (x % 5) - 2;
                    float delta = ((x + (2 * y)) % 7) - 3;
                    referenceSamples[(y * reference.PixelsPerRow) + x] = value;
                    distortedSamples[(y * distorted.PixelsPerRow) + x] = value - delta;
                }
            }

            for (int stage = 0; stage < 3; stage++)
            {
                // Quarter and half weights keep every expected result exact. The last call must overwrite the accumulated result.
                if (stage == 0)
                {
                    Butteraugli.SetL2Diff(reference, distorted, 0.25F, difference);
                }
                else if (stage == 1)
                {
                    Butteraugli.L2Diff(reference, distorted, 0.5F, difference);
                }
                else
                {
                    Butteraugli.SetL2Diff(reference, distorted, 0.5F, difference);
                }

                float expectedWeight = stage == 0 ? 0.25F : stage == 1 ? 0.75F : 0.5F;
                for (int y = 0; y < height; y++)
                {
                    ReadOnlySpan<float> row = differenceSamples.Slice(y * difference.PixelsPerRow, difference.PixelsPerRow);

                    for (int x = 0; x < sampleWidth; x++)
                    {
                        float delta = ((x + (2 * y)) % 7) - 3;
                        Assert.Equal(delta * delta * expectedWeight, row[x]);
                    }

                    for (int x = sampleWidth; x < row.Length; x++)
                    {
                        Assert.Equal(padding, row[x]);
                    }
                }
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            width,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Checks the separable five-tap path's reflected edges, exact sample values and untouched padding.
    /// </summary>
    /// <param name="width">The logical width, including the single-sample reflection case.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(17)]
    [InlineData(33)]
    public void Separable5_ReflectsImpulseAtImageEdges(int width)
    {
        static void RunTest(string serialized)
        {
            int sampleWidth = FeatureTestRunner.Deserialize<int>(serialized);
            const int height = 3;
            const float padding = -123F;
            using JxlPlane<float> input = new(TestEnvironment.Configuration, sampleWidth, height);
            using JxlPlane<float> output = new(TestEnvironment.Configuration, sampleWidth, height);
            Span<float> inputSamples = input.GetRow(0);
            Span<float> outputSamples = output.GetRow(0);
            inputSamples.Clear();
            outputSamples.Fill(padding);

            // Edge and interior impulses have row amplitudes 1, 2 and 3. The interior impulse exercises nonzero SIMD lanes.
            for (int y = 0; y < height; y++)
            {
                inputSamples[y * input.PixelsPerRow] = y + 1;
                if (sampleWidth >= 17)
                {
                    inputSamples[(y * input.PixelsPerRow) + (sampleWidth / 2)] = y + 1;
                }
            }

            JxlWeightsSeparable5 weights = default;
            for (int i = 0; i < 4; i++)
            {
                // The symmetric binomial kernel [1,4,6,4,1]/16 has exact weights and a unit sum.
                weights.Horizontal[i] = weights.Vertical[i] = 0.375F;
                weights.Horizontal[4 + i] = weights.Vertical[4 + i] = 0.25F;
                weights.Horizontal[8 + i] = weights.Vertical[8 + i] = 0.0625F;
            }

            Assert.True(JxlConvolve.Separable5(input, new Rectangle(0, 0, sampleWidth, height), weights, output));

            // Reflected rows produce [23,32,41]/16. The edge impulse distributes as [10,5,1]/16; a single-sample row retains one.
            // The separate interior impulse contributes [1,4,6,4,1]/16 without reflection. All expected values remain exact.
            ReadOnlySpan<float> vertical = [1.4375F, 2F, 2.5625F];
            for (int y = 0; y < height; y++)
            {
                ReadOnlySpan<float> row = outputSamples.Slice(y * output.PixelsPerRow, output.PixelsPerRow);

                for (int x = 0; x < sampleWidth; x++)
                {
                    float horizontal = sampleWidth == 1 ? 1F : x == 0 ? 0.625F : x == 1 ? 0.3125F : x == 2 ? 0.0625F : 0F;
                    if (sampleWidth >= 17)
                    {
                        int distance = Math.Abs(x - (sampleWidth / 2));
                        horizontal += distance == 0 ? 0.375F : distance == 1 ? 0.25F : distance == 2 ? 0.0625F : 0F;
                    }

                    Assert.Equal(vertical[y] * horizontal, row[x]);
                }

                for (int x = sampleWidth; x < row.Length; x++)
                {
                    Assert.Equal(padding, row[x]);
                }
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            width,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    private static JxlImage3<float> SinglePixelImage(float r, float g, float b)
    {
        JxlImage3<float> img = new(TestEnvironment.Configuration, 1, 1);
        img.PlaneRow(0, 0)[0] = r;
        img.PlaneRow(1, 0)[0] = r;
        img.PlaneRow(2, 0)[0] = r;
        return img;
    }

    private static void AddUniformNoise(JxlImage3<float> img, float d, ulong seed)
    {
        Rng generator = new(seed);

        for (int y = 0; y < img.YSize; ++y)
        {
            for (int c = 0; c < 3; ++c)
            {
                Span<float> planeRow = img.PlaneRow(c, y);

                for (int x = 0; x < img.XSize; ++x)
                {
                    planeRow[x] += generator.UniformF(-d, d);
                }
            }
        }
    }

    private static void AddEdge(JxlImage3<float> image, float d, int x0, int y0)
    {
        int h = Math.Min(image.YSize - y0, 100);
        int w = Math.Min(image.XSize - x0, 5);

        for (int dy = 0; dy < h; ++dy)
        {
            Span<float> planeRow = image.PlaneRow(1, y0 + dy);

            for (int dx = 0; dx < w; ++dx)
            {
                planeRow[x0 + dx] += d;
            }
        }
    }

    [Fact]
    public void TestSinglePixel()
    {
        JxlImage3<float> rgb0 = SinglePixelImage(0.5f, 0.5f, 0.5f);
        JxlImage3<float> rgb1 = SinglePixelImage(0.5f, 0.49f, 0.5f);

        ButteraugliParameters butteraugliParameters = new();
        JxlPlane<float> diffmap = new();

        Assert.True(
            Butteraugli.ButteraugliInterface(TestEnvironment.Configuration, rgb0, rgb1, butteraugliParameters, diffmap, out double diffval),
            "Butteraugli initialization failed");

        Assert.True(new TolerantMath(0.5).AreEqual(diffval, 2.5), $"Diff value isn't even close to 2.5 (it's {diffval})");

        JxlPlane<float> diffmap2 = new();
        Assert.True(Butteraugli.ButteraugliInterfaceInPlace(
            TestEnvironment.Configuration,
            rgb0,
            rgb1,
            butteraugliParameters,
            diffmap2,
            out double diffval2));

        Assert.True(new TolerantMath(1e-10).AreEqual(diffval, diffval2), $"Diff value isn't even close to diffval2 (diffval={diffval}, diffval2={diffval2})");
    }

    // TODO: we need to port test image stuff from libjxl
    [Fact]
    public void TestLargeImage()
    {
        const int xSize = 1024;
        const int ySize = 1024;

        JxlTestImage img = new();
        img.SetDimensions(xSize, ySize);

        JxlTestFrame frame = img.AddFrame();
        frame.RandomFill(777);

        JxlImage3<float> rgb0 = GetColorImage(img.Ppf);
        JxlImage3<float> rgb1 = new(TestEnvironment.Configuration, xSize, ySize);
        JxlImageOperations.CopyImage(rgb0, rgb1);

        AddUniformNoise(rgb1, 0.02f, 7777uL);
        AddEdge(rgb1, 0.1f, xSize / 2, xSize / 2);

        ButteraugliParameters butteraugliParameters = new();
        JxlPlane<float> diffmap = new();
        Assert.True(
            Butteraugli.ButteraugliInterface(TestEnvironment.Configuration,, rgb0, rgb1, butteraugliParameters, diffmap, out double diffval),
            "Couldn't initialize Butteraugli");

        double distp = Butteraugli.ComputeDistanceP(diffmap, butteraugliParameters, 3.0);
        Assert.True(new TolerantMath(0.5).AreEqual(diffval, 4.0), $"Diff isn't even close to 4.0 (diffval={diffval})");
        Assert.True(new TolerantMath(0.5).AreEqual(distp, 1.5), $"Distance isn't even close to 4.0 (distp={distp})");

        JxlPlane<float> diffmap2 = new();
        Assert.True(
            Butteraugli.ButteraugliInterfaceInPlace(
                TestEnvironment.Configuration,
                rgb0,
                rgb1,
                butteraugliParameters,
                diffmap2,
                out double diffval2),
            "Butteraugli in-place interface initialization failed");

        double distp2 = Butteraugli.ComputeDistanceP(diffmap2, butteraugliParameters, 3.0);

        Assert.True(new TolerantMath(5e-7).AreEqual(diffval, diffval2), $"Diffval != diffval2 (diffval={diffval}, diffval2={diffval2})");
        Assert.True(new TolerantMath(1e-7).AreEqual(distp, distp2), $"Distp != distp2 (distp={distp}, distp2={distp2})");
    }
}
