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
            double maltaWeight = changedComponent == 0 ? 8246.75321353D : 37.0819870399D;
            double normalization = changedComponent == 0 ? 1009002.70582D : 130262059.556D;
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

    /// <summary>
    /// Checks native accumulation order, odd edges, input preservation and logical output bounds at every SIMD width.
    /// </summary>
    [Fact]
    public void SubSample2x_PreservesContributionOrderAndNormalizesOddEdges()
    {
        static void RunTest()
        {
            const float padding = -123F;
            ReadOnlySpan<Size> sizes = [new(1, 1), new(3, 1), new(1, 3), new(6, 4), new(34, 6), new(35, 7), new(66, 4)];

            foreach (Size size in sizes)
            {
                using JxlImage3<float> input = new(TestEnvironment.Configuration, size.Width, size.Height);
                using JxlImage3<float> output = new(TestEnvironment.Configuration, (size.Width + 1) / 2, (size.Height + 1) / 2);

                for (int c = 0; c < 3; c++)
                {
                    JxlPlane<float> inputPlane = input.Plane(c);
                    JxlPlane<float> outputPlane = output.Plane(c);
                    Span<float> source = inputPlane.GetRow(0);
                    source.Fill(padding);
                    outputPlane.GetRow(0).Fill(padding);

                    for (int y = 0; y < size.Height; y++)
                    {
                        for (int x = 0; x < size.Width; x++)
                        {
                            // The first plane distinguishes chronological addition from a regrouped average: complete
                            // groups produce 3/4096, rather than the exact-arithmetic mean 1/1024. All samples stay within the HDR range.
                            // The third plane uses negative zero to check the initial positive-zero addition.
                            source[(y * inputPlane.PixelsPerRow) + x] = c == 0
                                ? (((y & 1) * 2) + (x & 1)) switch
                                {
                                    0 => 32768F,
                                    1 => 1F / 1024F,
                                    2 => -32768F,
                                    _ => 3F / 1024F
                                }
                                : c == 1 ? ((x * 3) - (y * 5) + 1) * 0.25F : BitConverter.Int32BitsToSingle(int.MinValue);

                        }
                    }
                }

                Butteraugli.SubSample2x(input, output);

                for (int c = 0; c < 3; c++)
                {
                    JxlPlane<float> inputPlane = input.Plane(c);
                    JxlPlane<float> outputPlane = output.Plane(c);
                    ReadOnlySpan<float> source = inputPlane.GetRow(0);
                    ReadOnlySpan<float> destination = outputPlane.GetRow(0);

                    for (int y = 0; y < output.YSize; y++)
                    {
                        for (int x = 0; x < output.XSize; x++)
                        {
                            // Use the native source-pixel visitation order as an independent scalar oracle. Each available
                            // source contributes one quarter before the unmatched column and row are doubled, in that order.
                            float expected = 0F;
                            for (int dy = 0; dy < 2 && ((2 * y) + dy) < size.Height; dy++)
                            {
                                for (int dx = 0; dx < 2 && ((2 * x) + dx) < size.Width; dx++)
                                {
                                    expected += 0.25F * source[(((2 * y) + dy) * inputPlane.PixelsPerRow) + (2 * x) + dx];
                                }
                            }

                            if ((size.Width & 1) != 0 && x == output.XSize - 1)
                            {
                                expected *= 2F;
                            }

                            if ((size.Height & 1) != 0 && y == output.YSize - 1)
                            {
                                expected *= 2F;
                            }

                            Assert.Equal(
                                BitConverter.SingleToInt32Bits(expected),
                                BitConverter.SingleToInt32Bits(destination[(y * outputPlane.PixelsPerRow) + x]));

                        }

                        for (int x = output.XSize; x < outputPlane.PixelsPerRow; x++)
                        {
                            Assert.Equal(padding, destination[(y * outputPlane.PixelsPerRow) + x]);
                        }
                    }

                    for (int y = 0; y < size.Height; y++)
                    {
                        for (int x = 0; x < size.Width; x++)
                        {
                            float expected = c == 0
                                ? (((y & 1) * 2) + (x & 1)) switch
                                {
                                    0 => 32768F,
                                    1 => 1F / 1024F,
                                    2 => -32768F,
                                    _ => 3F / 1024F
                                }
                                : c == 1 ? ((x * 3) - (y * 5) + 1) * 0.25F : BitConverter.Int32BitsToSingle(int.MinValue);

                            Assert.Equal(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits(source[(y * inputPlane.PixelsPerRow) + x]));
                        }

                        for (int x = size.Width; x < inputPlane.PixelsPerRow; x++)
                        {
                            Assert.Equal(padding, source[(y * inputPlane.PixelsPerRow) + x]);
                        }
                    }
                }
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Checks native single-sample frequency equations and logical row bounds at every SIMD width.
    /// </summary>
    [Fact]
    public void FrequencySeparation_PreservesSingleSampleEquationsAndPadding()
    {
        static void RunTest()
        {
            const float padding = -123F;
            ReadOnlySpan<int> widths = [1, 3, 17, 33];
            ReadOnlySpan<(float X, float Y)> samples = [(-2F, -80F), (0.125F, 0.01F), (2F, 80F)];

            foreach (int width in widths)
            {
                using JxlImage3<float> mf = new(TestEnvironment.Configuration, width, 1);
                using JxlPlane<float> hfX = new(TestEnvironment.Configuration, width, 1);
                using JxlPlane<float> hfY = new(TestEnvironment.Configuration, width, 1);
                using JxlPlane<float> uhfX = new(TestEnvironment.Configuration, width, 1);
                using JxlPlane<float> uhfY = new(TestEnvironment.Configuration, width, 1);
                using ButteraugliBlurTemp blurTemp = new(TestEnvironment.Configuration, width, 1);
                InlineArray2<JxlPlane<float>> hf = default;
                InlineArray2<JxlPlane<float>> uhf = default;
                hf[0] = hfX;
                hf[1] = hfY;
                uhf[0] = uhfX;
                uhf[1] = uhfY;

                for (int sampleIndex = 0; sampleIndex < (width == 1 ? samples.Length : 1); sampleIndex++)
                {
                    // A single sample has only the Gaussian center weight, exactly one, in both passes. Its blur is
                    // therefore known independently of kernel construction. Wider zero samples exercise SIMD bounds only.
                    float inputX = width == 1 ? samples[sampleIndex].X : 0F;
                    float inputY = width == 1 ? samples[sampleIndex].Y : 0F;
                    for (int c = 0; c < 3; c++)
                    {
                        Span<float> values = mf.Plane(c).GetRow(0);
                        values.Fill(padding);
                        values[..width].Fill(c == 0 ? inputX : c == 1 ? inputY : 0F);
                    }

                    hfX.GetRow(0).Fill(padding);
                    hfY.GetRow(0).Fill(padding);
                    uhfX.GetRow(0).Fill(padding);
                    uhfY.GetRow(0).Fill(padding);
                    Assert.True(Butteraugli.SeparateMfAndHf(TestEnvironment.Configuration, mf, hf, blurTemp));
                    float mfX = MathF.CopySign(MathF.Max(MathF.Abs(inputX) - 0.29F, 0F), inputX);
                    float mfY = MathF.Abs(inputY) > 0.1F ? MathF.CopySign(MathF.Abs(inputY) + 0.1F, inputY) : inputY + inputY;
                    CheckPlane(mf.Plane(0), mfX);
                    CheckPlane(mf.Plane(1), mfY);
                    CheckPlane(mf.Plane(2), 0F);
                    CheckPlane(hfX, 0F);
                    CheckPlane(hfY, 0F);

                    hfX.GetRow(0)[..width].Fill(inputX);
                    hfY.GetRow(0)[..width].Fill(inputY);
                    Assert.True(Butteraugli.SeparateHFAndUHF(TestEnvironment.Configuration, hf, uhf, blurTemp));

                    // Native Y clips the blurred base BEFORE residual subtraction. Using the unclipped original would
                    // produce zero UHF here; values outside the base threshold must instead produce a nonzero residual.
                    const float slope = 0.724216145665F;
                    const float hfMaximum = 28.4691806922F;
                    const float uhfMaximum = 5.19175294647F;
                    float clippedY = inputY;
                    if (MathF.Abs(clippedY) > hfMaximum)
                    {
                        clippedY = MathF.CopySign(float.MultiplyAddEstimate(MathF.Abs(clippedY) - hfMaximum, slope, hfMaximum), clippedY);
                    }

                    float residualY = inputY - clippedY;
                    if (MathF.Abs(residualY) > uhfMaximum)
                    {
                        residualY = MathF.CopySign(float.MultiplyAddEstimate(MathF.Abs(residualY) - uhfMaximum, slope, uhfMaximum), residualY);
                    }

                    float scaledY = clippedY * 2.155F;
                    float hfResultY = MathF.Abs(scaledY) > 0.132F
                        ? MathF.CopySign(MathF.Abs(scaledY) + 0.132F, scaledY)
                        : scaledY + scaledY;

                    CheckPlane(hfX, MathF.CopySign(MathF.Max(MathF.Abs(inputX) - 1.5F, 0F), inputX));
                    CheckPlane(hfY, hfResultY);
                    CheckPlane(uhfX, 0F);
                    CheckPlane(uhfY, residualY * 2.69313763794F);
                }
            }

            // Check each logical result and the padded suffix separately. Padding belongs to the plane, not the frequency calculation.
            static void CheckPlane(JxlPlane<float> plane, float expected)
            {
                ReadOnlySpan<float> values = plane.GetRow(0);
                for (int x = 0; x < plane.XSize; x++)
                {
                    Assert.Equal(expected, values[x]);
                }

                for (int x = plane.XSize; x < plane.PixelsPerRow; x++)
                {
                    Assert.Equal(-123F, values[x]);
                }
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>Checks native comparison-opsin equations, zero and RGB basis fields, in-place output and logical guards at all SIMD widths.</summary>
    [Fact]
    public void OpsinDynamics_UsesNativeResponseAndPreservesLogicalBounds()
    {
        static void RunTest()
        {
            ReadOnlySpan<int> widths = [1, 3, 17, 33];
            ReadOnlySpan<float> weights =
            [
                0.29956550340058319F, 0.63373087833825936F, 0.077705617820981968F, 1.7557483643287353F,
                0.22158691104574774F, 0.69391388044116142F, 0.0987313588422F, 1.7557483643287353F,
                0.02F, 0.02F, 0.20480129041026129F, 12.226454707163354F
            ];

            const int height = 3;
            const float untouched = -123F;

            // Native's log2 test allows 3.1e-6 logarithm units. Two gamma terms amplify that to less than 0.000083 comparison units.
            // Round the absolute component bound to 0.0001 to include the remaining float blur, division and multiplication rounding.
            const float responseBound = 0.0001F;
            Configuration configuration = TestEnvironment.Configuration;
            ButteraugliParameters parameters = new();
            foreach (int width in widths)
            {
                using JxlImage3<float> source = new(configuration, width + 19, height + 2);
                using JxlImage3<float> blurred = new(configuration, width + 35, height + 2);
                using JxlImage3<float> transformed = new(configuration, width + 51, height + 2);
                Assert.True(source.ShrinkTo(width, height));
                Assert.True(blurred.ShrinkTo(width, height));
                Assert.True(transformed.ShrinkTo(width, height));
                using ButteraugliBlurTemp scratch = new(configuration, width, height);
                Span<float> source0 = source.PlaneRow(0, 0);
                Span<float> source1 = source.PlaneRow(1, 0);
                Span<float> source2 = source.PlaneRow(2, 0);
                Span<float> blurred0 = blurred.PlaneRow(0, 0);
                Span<float> blurred1 = blurred.PlaneRow(1, 0);
                Span<float> blurred2 = blurred.PlaneRow(2, 0);
                Span<float> output0 = transformed.PlaneRow(0, 0);
                Span<float> output1 = transformed.PlaneRow(1, 0);
                Span<float> output2 = transformed.PlaneRow(2, 0);

                // Zero and three constant RGB basis fields remain constant under the normalized mirrored blur.
                // The larger retained allocations give source, blur and output different physical row strides.
                for (int basis = 0; basis < 4; basis++)
                {
                    source0.Fill(untouched);
                    source1.Fill(untouched);
                    source2.Fill(untouched);
                    blurred0.Fill(untouched);
                    blurred1.Fill(untouched);
                    blurred2.Fill(untouched);
                    output0.Fill(untouched);
                    output1.Fill(untouched);
                    output2.Fill(untouched);
                    for (int c = 0; c < 3; c++)
                    {
                        Span<float> samples = c == 0 ? source0 : c == 1 ? source1 : source2;
                        float value = basis == c + 1 ? 0.25F : 0F;
                        for (int y = 0; y < height; y++)
                        {
                            samples.Slice(y * source.PixelsPerRow, width).Fill(value);
                        }
                    }

                    float red = (basis == 1 ? 0.25F : 0F) * parameters.IntensityTarget;
                    float green = (basis == 2 ? 0.25F : 0F) * parameters.IntensityTarget;
                    float blue = (basis == 3 ? 0.25F : 0F) * parameters.IntensityTarget;
                    InlineArray3<float> response = default;
                    for (int c = 0; c < 3; c++)
                    {
                        // Evaluate native equations with independent frozen weights and the mathematical logarithm.
                        // No production opsin helper or output sample contributes to this reference calculation.
                        float absorbance = float.MultiplyAddEstimate(weights[(c * 4) + 2], blue, weights[(c * 4) + 3]);
                        absorbance = float.MultiplyAddEstimate(weights[(c * 4) + 1], green, absorbance);
                        absorbance = float.MultiplyAddEstimate(weights[c * 4], red, absorbance);
                        float pre = MathF.Max(absorbance, weights[(c * 4) + 3]);
                        float gamma = float.MultiplyAddEstimate(
                            19.245013259874995F * 0.6931471805599453F,
                            MathF.Log2(pre + 9.9710635769299145F),
                            -23.16046239805755F);

                        float sensitivity = MathF.Max(gamma / pre, 1e-4F);
                        response[c] = MathF.Max(absorbance * sensitivity, weights[(c * 4) + 3]);
                    }

                    Assert.True(Butteraugli.OpsinDynamicsImage(configuration, source, parameters, blurred, scratch, transformed));
                    for (int c = 0; c < 3; c++)
                    {
                        float expected = c == 0 ? response[0] - response[1] : c == 1 ? response[0] + response[1] : response[2];
                        ReadOnlySpan<float> actual = c == 0 ? output0 : c == 1 ? output1 : output2;
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                Assert.Equal(expected, actual[(y * transformed.PixelsPerRow) + x], responseBound);
                            }
                        }
                    }

                    // Reuse the prepared scratch and source allocation. In-place execution must produce the same samples as disjoint output.
                    Assert.True(Butteraugli.OpsinDynamicsImage(configuration, source, parameters, blurred, scratch, source));
                    for (int c = 0; c < 3; c++)
                    {
                        ReadOnlySpan<float> input = c == 0 ? source0 : c == 1 ? source1 : source2;
                        ReadOnlySpan<float> actual = c == 0 ? output0 : c == 1 ? output1 : output2;
                        ReadOnlySpan<float> smooth = c == 0 ? blurred0 : c == 1 ? blurred1 : blurred2;
                        for (int i = 0; i < input.Length; i++)
                        {
                            int y = i / source.PixelsPerRow;
                            int x = i % source.PixelsPerRow;
                            if (y < height && x < width)
                            {
                                Assert.Equal(actual[(y * transformed.PixelsPerRow) + x], input[i]);
                            }
                            else
                            {
                                Assert.Equal(untouched, input[i]);
                            }
                        }

                        for (int i = 0; i < actual.Length; i++)
                        {
                            if (i / transformed.PixelsPerRow >= height || i % transformed.PixelsPerRow >= width)
                            {
                                Assert.Equal(untouched, actual[i]);
                            }
                        }

                        for (int i = 0; i < smooth.Length; i++)
                        {
                            if (i / blurred.PixelsPerRow >= height || i % blurred.PixelsPerRow >= width)
                            {
                                Assert.Equal(untouched, smooth[i]);
                            }
                        }
                    }
                }
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Checks native Malta ray responses, zero-extension, accumulation and logical bounds through the retained comparison API.
    /// </summary>
    [Fact]
    public void MaltaDifference_AccumulatesKnownImpulseRaysAndPreservesPadding()
    {
        static void RunTest()
        {
            const float padding = -123F;
            ReadOnlySpan<Size> sizes = [new(8, 8), new(13, 9), new(17, 9), new(33, 9), new(41, 10)];
            ReadOnlySpan<(int X, int Y, int HfRays, int LfRays)> checks =
            [
                (0, 0, 16, 16), (4, 0, 1, 1), (1, 0, 5, 0), (3, 3, 1, 1), (4, 4, 0, 0),
                (-4, 0, 1, 1), (-1, 0, 5, 0), (-3, -3, 1, 1), (-4, -4, 0, 0), (5, 0, 0, 0)
            ];

            foreach (Size size in sizes)
            {
                using JxlPlane<float> reference = new(TestEnvironment.Configuration, size.Width + 3, size.Height);
                using JxlPlane<float> distorted = new(TestEnvironment.Configuration, size.Width + 19, size.Height);
                using JxlPlane<float> differences = new(TestEnvironment.Configuration, size.Width + 35, size.Height);
                using JxlPlane<float> output = new(TestEnvironment.Configuration, size.Width + 51, size.Height);
                reference.ShrinkTo(size.Width, size.Height);
                distorted.ShrinkTo(size.Width, size.Height);
                differences.ShrinkTo(size.Width, size.Height);
                output.ShrinkTo(size.Width, size.Height);
                Span<float> referenceSamples = reference.GetRow(0);
                Span<float> distortedSamples = distorted.GetRow(0);
                Span<float> differenceSamples = differences.GetRow(0);
                Span<float> outputSamples = output.GetRow(0);

                for (int mode = 0; mode < 2; mode++)
                {
                    bool lowFrequency = mode != 0;
                    // With w0Gt1=2 and norm1=0.5, a reference impulse of one has denominator one. Native setup gives
                    // amplitude mulli/17, rounded once: HF=0x3CC04C91 and LF=0x3D135CD7. The secondary penalty is disabled.
                    float amplitude = BitConverter.Int32BitsToSingle(lowFrequency ? 0x3D135CD7 : 0x3CC04C91);
                    for (int position = 0; position < 2; position++)
                    {
                        int impulseX = position == 0 ? size.Width / 2 : 0;
                        int impulseY = position == 0 ? size.Height / 2 : 0;
                        referenceSamples.Fill(padding);
                        distortedSamples.Fill(padding);
                        differenceSamples.Fill(padding);
                        outputSamples.Fill(padding);
                        for (int y = 0; y < size.Height; y++)
                        {
                            referenceSamples.Slice(y * reference.PixelsPerRow, size.Width).Clear();
                            distortedSamples.Slice(y * distorted.PixelsPerRow, size.Width).Clear();
                            outputSamples.Slice(y * output.PixelsPerRow, size.Width).Fill(7F);
                        }

                        referenceSamples[(impulseY * reference.PixelsPerRow) + impulseX] = 1F;
                        Assert.True(Butteraugli.MaltaDiffMap(lowFrequency, reference, distorted, 2D, 0D, 0.5D, differences, output));

                        // Every ray contains its center. Exactly one horizontal ray reaches offset (4,0); five full rays
                        // cross (1,0), which sparse LF rays omit. The diagonal ray ends at (3,3), not the absent (4,4).
                        // A single impulse therefore needs only the known ray multiplicity, not another stencil implementation.
                        foreach ((int dx, int dy, int hfRays, int lfRays) in checks)
                        {
                            int x = impulseX + dx;
                            int y = impulseY + dy;
                            if ((uint)x < (uint)size.Width && (uint)y < (uint)size.Height)
                            {
                                int rays = lowFrequency ? lfRays : hfRays;
                                float energy = 0F;
                                for (int ray = 0; ray < rays; ray++)
                                {
                                    energy = float.MultiplyAddEstimate(amplitude, amplitude, energy);
                                }

                                Assert.Equal(7F + energy, outputSamples[(y * output.PixelsPerRow) + x]);
                            }
                        }

                        for (int y = 0; y < size.Height; y++)
                        {
                            for (int x = 0; x < size.Width; x++)
                            {
                                bool impulse = x == impulseX && y == impulseY;
                                Assert.Equal(impulse ? 1F : 0F, referenceSamples[(y * reference.PixelsPerRow) + x]);
                                Assert.Equal(0F, distortedSamples[(y * distorted.PixelsPerRow) + x]);
                                Assert.Equal(impulse ? amplitude : 0F, differenceSamples[(y * differences.PixelsPerRow) + x]);
                            }

                            Assert.Equal(
                                -1,
                                referenceSamples.Slice((y * reference.PixelsPerRow) + size.Width, reference.PixelsPerRow - size.Width)
                                    .IndexOfAnyExcept(padding));

                            Assert.Equal(
                                -1,
                                distortedSamples.Slice((y * distorted.PixelsPerRow) + size.Width, distorted.PixelsPerRow - size.Width)
                                    .IndexOfAnyExcept(padding));

                            Assert.Equal(
                                -1,
                                differenceSamples.Slice((y * differences.PixelsPerRow) + size.Width, differences.PixelsPerRow - size.Width)
                                    .IndexOfAnyExcept(padding));

                            Assert.Equal(
                                -1,
                                outputSamples.Slice((y * output.PixelsPerRow) + size.Width, output.PixelsPerRow - size.Width).IndexOfAnyExcept(padding));

                        }
                    }
                }
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Checks nonzero masking magnitudes across different physical strides, all SIMD widths and scalar tails.
    /// </summary>
    [Fact]
    public void MaskingMagnitude_CombinesFourBandsWithoutChangingPadding()
    {
        static void RunTest()
        {
            const float padding = -123F;
            ReadOnlySpan<int> widths = [1, 3, 17, 33];
            foreach (int width in widths)
            {
                using JxlPlane<float> hfX = new(TestEnvironment.Configuration, width + 3, 3);
                using JxlPlane<float> hfY = new(TestEnvironment.Configuration, width + 19, 3);
                using JxlPlane<float> uhfX = new(TestEnvironment.Configuration, width + 35, 3);
                using JxlPlane<float> uhfY = new(TestEnvironment.Configuration, width + 51, 3);
                using JxlPlane<float> output = new(TestEnvironment.Configuration, width, 3);
                hfX.ShrinkTo(width, 3);
                hfY.ShrinkTo(width, 3);
                uhfX.ShrinkTo(width, 3);
                uhfY.ShrinkTo(width, 3);
                InlineArray2<JxlPlane<float>> hf = default;
                InlineArray2<JxlPlane<float>> uhf = default;
                hf[0] = hfX;
                hf[1] = hfY;
                uhf[0] = uhfX;
                uhf[1] = uhfY;
                Span<float> hfXSamples = hfX.GetRow(0);
                Span<float> hfYSamples = hfY.GetRow(0);
                Span<float> uhfXSamples = uhfX.GetRow(0);
                Span<float> uhfYSamples = uhfY.GetRow(0);
                Span<float> outputSamples = output.GetRow(0);
                hfXSamples.Fill(padding);
                hfYSamples.Fill(padding);
                uhfXSamples.Fill(padding);
                uhfYSamples.Fill(padding);
                outputSamples.Fill(padding);

                for (int y = 0; y < 3; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float xSign = (x & 1) == 0 ? 1F : -1F;
                        float ySign = ((x + y) & 1) == 0 ? 1F : -1F;
                        hfXSamples[(y * hfX.PixelsPerRow) + x] = xSign;
                        uhfXSamples[(y * uhfX.PixelsPerRow) + x] = xSign;
                        hfYSamples[(y * hfY.PixelsPerRow) + x] = 22.5F * ySign;
                        uhfYSamples[(y * uhfY.PixelsPerRow) + x] = 7.5F * ySign;
                    }
                }

                Butteraugli.CombineChannelsForMasking(hf, uhf, output);

                // X gives magnitude 5; the separate Y products give 3 + 9 = 12. The known 5-12-13 triangle is independent
                // of the SIMD implementation and checks both signs without copying its square/square-root expression.
                for (int y = 0; y < 3; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        float xSign = (x & 1) == 0 ? 1F : -1F;
                        float ySign = ((x + y) & 1) == 0 ? 1F : -1F;
                        Assert.Equal(13F, outputSamples[(y * output.PixelsPerRow) + x]);
                        Assert.Equal(xSign, hfXSamples[(y * hfX.PixelsPerRow) + x]);
                        Assert.Equal(xSign, uhfXSamples[(y * uhfX.PixelsPerRow) + x]);
                        Assert.Equal(22.5F * ySign, hfYSamples[(y * hfY.PixelsPerRow) + x]);
                        Assert.Equal(7.5F * ySign, uhfYSamples[(y * uhfY.PixelsPerRow) + x]);
                    }

                    Assert.Equal(-1, hfXSamples.Slice((y * hfX.PixelsPerRow) + width, hfX.PixelsPerRow - width).IndexOfAnyExcept(padding));
                    Assert.Equal(-1, hfYSamples.Slice((y * hfY.PixelsPerRow) + width, hfY.PixelsPerRow - width).IndexOfAnyExcept(padding));
                    Assert.Equal(-1, uhfXSamples.Slice((y * uhfX.PixelsPerRow) + width, uhfX.PixelsPerRow - width).IndexOfAnyExcept(padding));
                    Assert.Equal(-1, uhfYSamples.Slice((y * uhfY.PixelsPerRow) + width, uhfY.PixelsPerRow - width).IndexOfAnyExcept(padding));
                    Assert.Equal(-1, outputSamples.Slice((y * output.PixelsPerRow) + width, output.PixelsPerRow - width).IndexOfAnyExcept(padding));
                }
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }

    /// <summary>
    /// Checks the float directional-penalty contract through both retained Malta APIs at every SIMD width.
    /// </summary>
    [Fact]
    public void MaltaDifference_PreparesBothPenaltyDirectionsWithFloatArithmetic()
    {
        static void RunTest()
        {
            const float padding = -123F;
            ReadOnlySpan<int> widths = [13, 17, 33];
            ReadOnlySpan<(float Reference, float Actual, float Denominator, float Difference, float Penalty)> cases =
            [
                (1F, 0F, 1F, 1F, 0.55F), (-1F, 0F, 1F, -1F, -0.55F),
                (1F, 2F, 2F, -1F, 1.05F - 2F), (-1F, -2F, 2F, 1F, 2F - 1.05F),
                (1F, 1F, 1.5F, 0F, 0F), (-1F, -1F, 1.5F, 0F, 0F),
                (0F, 1F, 1F, -1F, -1F), (0F, -1F, 1F, 1F, 1F),
                (1F, 0.5F, 1.25F, 0.5F, 0.55F - 0.5F), (-1F, -0.5F, 1.25F, -0.5F, 0.5F - 0.55F)
            ];

            foreach (int width in widths)
            {
                using JxlPlane<float> reference = new(TestEnvironment.Configuration, width, 8);
                using JxlPlane<float> distorted = new(TestEnvironment.Configuration, width, 8);
                using JxlPlane<float> differences = new(TestEnvironment.Configuration, width, 8);
                using JxlPlane<float> output = new(TestEnvironment.Configuration, width, 8);
                Span<float> referenceSamples = reference.GetRow(0);
                Span<float> distortedSamples = distorted.GetRow(0);
                Span<float> differenceSamples = differences.GetRow(0);
                Span<float> outputSamples = output.GetRow(0);
                referenceSamples.Fill(padding);
                distortedSamples.Fill(padding);
                for (int y = 0; y < 8; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        var sample = cases[(x + y) % cases.Length];
                        referenceSamples[(y * reference.PixelsPerRow) + x] = sample.Reference;
                        distortedSamples[(y * distorted.PixelsPerRow) + x] = sample.Actual;
                    }
                }

                for (int mode = 0; mode < 2; mode++)
                {
                    bool lowFrequency = mode != 0;
                    // These bits are native double setup for weights 2,2 and normalization 0.5, rounded once to float.
                    // They are independent of the per-sample float thresholds and final multiply-add checked below.
                    float firstCoefficient = BitConverter.Int32BitsToSingle(lowFrequency ? 0x3D135CD7 : 0x3CC04C91);
                    float secondCoefficient = BitConverter.Int32BitsToSingle(lowFrequency ? 0x3CEF6F9C : 0x3C9C397A);
                    differenceSamples.Fill(padding);
                    outputSamples.Fill(padding);
                    output.Clear();
                    Assert.True(Butteraugli.MaltaDiffMap(lowFrequency, reference, distorted, 2D, 2D, 0.5D, differences, output));

                    for (int y = 0; y < 8; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            var sample = cases[(x + y) % cases.Length];
                            float primary = (firstCoefficient / sample.Denominator) * sample.Difference;
                            float expected = float.MultiplyAddEstimate(secondCoefficient / sample.Denominator, sample.Penalty, primary);
                            Assert.Equal(expected, differenceSamples[(y * differences.PixelsPerRow) + x]);
                        }

                        Assert.Equal(
                            -1,
                            differenceSamples.Slice((y * differences.PixelsPerRow) + width, differences.PixelsPerRow - width).IndexOfAnyExcept(padding));

                        Assert.Equal(-1, outputSamples.Slice((y * output.PixelsPerRow) + width, output.PixelsPerRow - width).IndexOfAnyExcept(padding));
                    }
                }
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic);
    }
}
