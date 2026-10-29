// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Memory;
using SixLabors.ImageSharp.Formats.Jxl.Processing;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Butteraugli;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;
using SixLabors.ImageSharp.Tests.Memory;
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
    /// Checks the native suppression endpoints, signed Y symmetry and exact logical row bounds at each SIMD width.
    /// </summary>
    [Fact]
    public void SuppressXByY_PreservesZeroYAndApproachesMinimumResponse()
    {
        static void RunTest()
        {
            const float padding = -123F;
            // At |Y| = 2^20, the variable response is less than half a float ULP at the native minimum response.
            // Both signs must therefore round to the same minimum, while zero Y must retain X unchanged.
            const float largeY = 1048576F;
            const float minimumResponse = 0.653020556257F;
            ReadOnlySpan<int> widths = [1, 3, 17, 33];

            foreach (int width in widths)
            {
                using JxlPlane<float> inputY = new(TestEnvironment.Configuration, width, 3);
                using JxlPlane<float> inputOutputX = new(TestEnvironment.Configuration, width, 3);
                Span<float> ySamples = inputY.GetRow(0);
                Span<float> xSamples = inputOutputX.GetRow(0);
                ySamples.Fill(padding);
                xSamples.Fill(padding);

                for (int y = 0; y < 3; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        ySamples[(y * inputY.PixelsPerRow) + x] = y == 0 ? 0F : y == 1 ? largeY : -largeY;
                        xSamples[(y * inputOutputX.PixelsPerRow) + x] = ((x % 5) - 2) * 0.25F;
                    }
                }

                Assert.True(Butteraugli.SuppressXByY(inputY, inputOutputX));

                for (int y = 0; y < 3; y++)
                {
                    ReadOnlySpan<float> row = xSamples.Slice(y * inputOutputX.PixelsPerRow, inputOutputX.PixelsPerRow);

                    for (int x = 0; x < width; x++)
                    {
                        float original = ((x % 5) - 2) * 0.25F;
                        Assert.Equal(y == 0 ? original : original * minimumResponse, row[x]);
                    }

                    for (int x = width; x < row.Length; x++)
                    {
                        Assert.Equal(padding, row[x]);
                    }
                }
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Checks standalone output preparation, separate half-resolution results and allocator cleanup for identical samples.
    /// </summary>
    [Fact]
    public void InPlaceInterface_PreparesOutputAndRetainsSubsampledResult()
    {
        static void RunTest()
        {
            Configuration configuration = TestEnvironment.Configuration.Clone();
            TestMemoryAllocator allocator = new();
            allocator.EnableNonThreadSafeLogging();
            configuration.MemoryAllocator = allocator;
            ReadOnlySpan<int> heights = [8, 17];

            foreach (int height in heights)
            {
                // Height 8 selects only the full-size comparison. Height 17 also produces and merges a half-size map.
                // Start with empty or undersized output to exercise this entry point's ownership, rather than its raw kernel.
                using (JxlImage3<float> reference = new(configuration, 17, height))
                using (JxlImage3<float> distorted = new(configuration, 17, height))
                using (JxlPlane<float> difference = new(configuration, height == 8 ? 0 : 1, height == 8 ? 0 : 1))
                {
                    JxlImageOperations.ZeroFillImage(reference);
                    JxlImageOperations.ZeroFillImage(distorted);
                    Assert.True(Butteraugli.ButteraugliInterfaceInPlace(
                        configuration,
                        reference,
                        distorted,
                        new ButteraugliParameters(),
                        difference,
                        out double score));

                    Assert.Equal(0D, score);
                    Assert.Equal(17, difference.XSize);
                    Assert.Equal(height, difference.YSize);
                    ReadOnlySpan<float> samples = difference.GetRow(0);

                    for (int y = 0; y < height; y++)
                    {
                        Assert.Equal(-1, samples.Slice(y * difference.PixelsPerRow, difference.XSize).IndexOfAnyExcept(0F));
                    }
                }

                // The old output allocation and both comparison levels must be returned when the outer owners are disposed.
                Assert.Equal(0, allocator.LiveBufferBytes);
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

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

    /// <summary>
    /// Checks that standalone comparison reads initialized DC and AC errors and leaves output padding unchanged.
    /// </summary>
    /// <param name="width">The row width, including SIMD tails.</param>
    [Theory]
    [InlineData(17)]
    [InlineData(33)]
    public void InPlaceComparison_IdenticalSamplesHaveZeroDifference(int width)
    {
        static void RunTest(string serialized)
        {
            int sampleWidth = FeatureTestRunner.Deserialize<int>(serialized);
            const int height = 8;
            const float padding = -123F;
            Configuration configuration = TestEnvironment.Configuration.Clone();
            configuration.MemoryAllocator = new TestMemoryAllocator();
            using JxlImage3<float> reference = new(configuration, sampleWidth, height);
            using JxlImage3<float> distorted = new(configuration, sampleWidth, height);
            using JxlPlane<float> difference = new(configuration, sampleWidth, height);
            JxlImageOperations.ZeroFillImage(reference);
            JxlImageOperations.ZeroFillImage(distorted);
            Span<float> result = difference.GetRow(0);
            result.Fill(padding);

            // Equal samples have zero error in every frequency band. Dirty allocations expose an uninitialized AC result.
            Assert.True(Butteraugli.ButteraugliDiffmapInPlace(configuration, reference, distorted, new ButteraugliParameters(), difference));

            for (int y = 0; y < height; y++)
            {
                ReadOnlySpan<float> row = result.Slice(y * difference.PixelsPerRow, difference.PixelsPerRow);
                for (int x = 0; x < sampleWidth; x++)
                {
                    Assert.Equal(0F, row[x]);
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

    /// <summary>
    /// Checks the retained comparison's middle-frequency orchestration against the native band weights and scaling.
    /// </summary>
    /// <param name="component">The X or Y middle-frequency component to change.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void PsychoComparison_MiddleFrequencyUsesNativeWeights(int component)
    {
        static void RunTest(string serialized)
        {
            int changedComponent = FeatureTestRunner.Deserialize<int>(serialized);
            const int width = 33;
            const int height = 8;
            Configuration configuration = TestEnvironment.Configuration;
            ButteraugliParameters parameters = new();
            using JxlImage3<float> reference = new(configuration, width, height);
            JxlImageOperations.ZeroFillImage(reference);
            using ButteraugliComparator comparator = ButteraugliComparator.Make(configuration, reference, parameters);
            using ButteraugliBlurTemp blur = new(configuration, width, height);
            using JxlImage3<float> blurred = new(configuration, width, height);
            using JxlImage3<float> transformed = new(configuration, width, height);
            using ButteraugliPsychoImage referenceBands = new();
            using ButteraugliPsychoImage distortedBands = new();
            referenceBands.Allocate(configuration, width, height);
            distortedBands.Allocate(configuration, width, height);
            Assert.True(Butteraugli.OpsinDynamicsImage(configuration, reference, parameters, blurred, blur, transformed));
            Assert.True(Butteraugli.SeparateFrequencies(configuration, blur, transformed, referenceBands));
            JxlImageOperations.CopyImage(referenceBands.Lf, distortedBands.Lf);
            JxlImageOperations.CopyImage(referenceBands.Mf, distortedBands.Mf);
            for (int c = 0; c < 2; c++)
            {
                JxlImageOperations.CopyImage(referenceBands.Hf[c], distortedBands.Hf[c]);
                JxlImageOperations.CopyImage(referenceBands.Uhf[c], distortedBands.Uhf[c]);
            }

            // Alter only one MF band. LF, HF and UHF remain identical, so their errors and mask correction are zero.
            Span<float> middleSamples = distortedBands.Mf.Plane(changedComponent).GetRow(0);
            int middleStride = distortedBands.Mf.Plane(changedComponent).PixelsPerRow;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    middleSamples[(y * middleStride) + x] += ((x % 3) - 1) * 0.125F;
                }
            }

            using JxlImage3<float> dc = new(configuration, width, height);
            using JxlImage3<float> ac = new(configuration, width, height);
            using JxlPlane<float> scratch = new(configuration, width, height);
            using JxlPlane<float> mask = new(configuration, width, height);
            using JxlPlane<float> expected = new(configuration, width, height);
            using JxlPlane<float> actual = new(configuration, width, height);
            JxlImageOperations.ZeroFillImage(dc);
            JxlImageOperations.ZeroFillImage(ac);

            // Native butteraugli.cc assigns these MF penalties to both error directions, independently of HF weights.
            // The named single-plane LF kernel owns native scaling 0.611612573796; the retained API must select that same kernel.
            float maltaWeight = changedComponent == 0 ? 8246.75321353F : 37.0819870399F;
            float normalization = changedComponent == 0 ? 1009002.70582F : 130262059.556F;
            float squaredErrorWeight = changedComponent == 0 ? 2150F : 10.6195433239F;
            Assert.True(Butteraugli.MaltaDiffMapLf(
                referenceBands.Mf.Plane(changedComponent),
                distortedBands.Mf.Plane(changedComponent),
                maltaWeight,
                maltaWeight,
                normalization,
                scratch,
                ac.Plane(changedComponent)));

            Butteraugli.L2Diff(
                referenceBands.Mf.Plane(changedComponent),
                distortedBands.Mf.Plane(changedComponent),
                squaredErrorWeight,
                ac.Plane(changedComponent));

            Assert.True(comparator.Mask(configuration, mask));
            Assert.True(Butteraugli.CombineChannelsToDiffmap(mask, dc, ac, 1F, expected));
            Assert.True(comparator.DiffmapPsychoImage(configuration, distortedBands, actual));
            ReadOnlySpan<float> expectedSamples = expected.GetRow(0);
            ReadOnlySpan<float> actualSamples = actual.GetRow(0);
            bool hasError = false;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float expectedValue = expectedSamples[(y * expected.PixelsPerRow) + x];
                    Assert.Equal(expectedValue, actualSamples[(y * actual.PixelsPerRow) + x]);
                    hasError |= expectedValue > 0;
                }
            }

            Assert.True(hasError);
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            component,
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
            Butteraugli.ButteraugliInterface(TestEnvironment.Configuration, rgb0, rgb1, butteraugliParameters, diffmap, out double diffval),
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

    /// <summary>
    /// Checks the native low-frequency basis weights, Y removal from B and exact row bounds at each SIMD width.
    /// </summary>
    [Fact]
    public void LowFrequencyToValues_UsesOriginalYAndPreservesPadding()
    {
        static void RunTest()
        {
            const float padding = -123F;
            const float xWeight = 33.832837186260F;
            const float yWeight = 14.458268100570F;
            const float bWeight = 49.87984651440F;
            const float yToBWeight = -0.362267051518F;
            ReadOnlySpan<int> widths = [1, 3, 17, 33];

            foreach (int width in widths)
            {
                using JxlImage3<float> image = new(TestEnvironment.Configuration, width, 3);
                JxlPlane<float> planeX = image.Plane(0);
                JxlPlane<float> planeY = image.Plane(1);
                JxlPlane<float> planeB = image.Plane(2);
                Span<float> samplesX = planeX.GetRow(0);
                Span<float> samplesY = planeY.GetRow(0);
                Span<float> samplesB = planeB.GetRow(0);
                samplesX.Fill(padding);
                samplesY.Fill(padding);
                samplesB.Fill(padding);

                // The first two rows isolate B and Y. The third cancels the native Y-to-B term exactly.
                // Dyadic samples keep that cancellation exact with fused or separate multiply-add instructions.
                for (int y = 0; y < 3; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float sample = ((x % 5) - 2) * 0.25F;
                        samplesX[(y * planeX.PixelsPerRow) + x] = sample;
                        samplesY[(y * planeY.PixelsPerRow) + x] = y == 0 ? 0F : sample;
                        samplesB[(y * planeB.PixelsPerRow) + x] = y == 0 ? sample : y == 1 ? 0F : -(sample * yToBWeight);
                    }
                }

                Butteraugli.XybLowFrequencyToValues(image);

                for (int y = 0; y < 3; y++)
                {
                    ReadOnlySpan<float> rowX = samplesX.Slice(y * planeX.PixelsPerRow, planeX.PixelsPerRow);
                    ReadOnlySpan<float> rowY = samplesY.Slice(y * planeY.PixelsPerRow, planeY.PixelsPerRow);
                    ReadOnlySpan<float> rowB = samplesB.Slice(y * planeB.PixelsPerRow, planeB.PixelsPerRow);

                    for (int x = 0; x < width; x++)
                    {
                        float sample = ((x % 5) - 2) * 0.25F;
                        Assert.Equal(sample * xWeight, rowX[x]);
                        Assert.Equal(y == 0 ? 0F : sample * yWeight, rowY[x]);
                        Assert.Equal(y == 0 ? sample * bWeight : y == 1 ? (sample * yToBWeight) * bWeight : 0F, rowB[x]);
                    }

                    for (int x = width; x < rowX.Length; x++)
                    {
                        Assert.Equal(padding, rowX[x]);
                    }

                    for (int x = width; x < rowY.Length; x++)
                    {
                        Assert.Equal(padding, rowY[x]);
                    }

                    for (int x = width; x < rowB.Length; x++)
                    {
                        Assert.Equal(padding, rowB[x]);
                    }
                }
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }
}
