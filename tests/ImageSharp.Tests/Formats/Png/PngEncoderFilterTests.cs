// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

// Uncomment this to turn unit tests into benchmarks:
// #define BENCHMARKING
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Png.Filters;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Png;

[Trait("Format", "Png")]
public class PngEncoderFilterTests : MeasureFixture
{
#if BENCHMARKING
    public const int Times = 1000000;
#else
    public const int Times = 1;
#endif

    public PngEncoderFilterTests(ITestOutputHelper output)
        : base(output)
    {
    }

    public const int Size = 64;

    /// <summary>
    /// Checks that the adaptive filter selection picks the filter with the smallest residual sum of
    /// <see cref="SubFilterOperator"/>, <see cref="UpFilterOperator"/>, <see cref="AverageFilterOperator"/> and <see cref="PaethFilterOperator"/>.
    /// Equal sums select the first of these, in this order.
    /// </summary>
    [Fact]
    public void SelectFilter_PicksTheSmallestSum()
    {
        static void RunTest()
        {
            foreach (int bytesPerPixel in new[] { 1, 2, 3, 4, 6, 8 })
            {
                // 1000 bytes reach the 512-bit, 256-bit and 128-bit loops and the scalar remainder.
                // Rows of one value give equal sums, so they also check the order of equal sums.
                Random random = new(bytesPerPixel);
                for (int row = 0; row < 32; row++)
                {
                    byte[] scanline = new byte[1000];
                    byte[] previousScanline = new byte[1000];
                    if (row % 8 == 0)
                    {
                        scanline.AsSpan().Fill((byte)row);
                        previousScanline.AsSpan().Fill((byte)row);
                    }
                    else
                    {
                        random.NextBytes(scanline);
                        random.NextBytes(previousScanline);
                    }

                    byte[] result = new byte[1001];
                    PngFilterEncoder.Encode<SubFilterOperator>(scanline, scanline, result, bytesPerPixel, out int sub);
                    PngFilterEncoder.Encode<UpFilterOperator>(scanline, previousScanline, result, 0, out int up);
                    PngFilterEncoder.Encode<AverageFilterOperator>(scanline, previousScanline, result, bytesPerPixel, out int average);
                    PngFilterEncoder.Encode<PaethFilterOperator>(scanline, previousScanline, result, bytesPerPixel, out int paeth);

                    int min = Math.Min(Math.Min(sub, up), Math.Min(average, paeth));
                    FilterType expected = sub == min ? FilterType.Sub : up == min ? FilterType.Up : average == min ? FilterType.Average : FilterType.Paeth;
                    Assert.Equal(expected, PngFilterEncoder.SelectFilter(scanline, previousScanline, bytesPerPixel));
                }
            }
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX2 | HwIntrinsics.DisableHWIntrinsic);
    }

    [Fact]
    public void Average()
    {
        static void RunTest()
        {
            TestData data = new(PngFilterMethod.Average, Size);
            data.TestFilter();
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.DisableHWIntrinsic);
    }

    [Fact]
    public void AverageSse2()
    {
        static void RunTest()
        {
            TestData data = new(PngFilterMethod.Average, Size);
            data.TestFilter();
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2 | HwIntrinsics.DisableHWIntrinsic);
    }

    [Fact]
    public void AverageSsse3()
    {
        static void RunTest()
        {
            TestData data = new(PngFilterMethod.Average, Size);
            data.TestFilter();
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(RunTest, HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2);
    }

    [Fact]
    public void AverageAvx2()
    {
        static void RunTest()
        {
            TestData data = new(PngFilterMethod.Average, Size);
            data.TestFilter();
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll);
    }

    [Fact]
    public void Paeth()
    {
        static void RunTest()
        {
            TestData data = new(PngFilterMethod.Paeth, Size);
            data.TestFilter();
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.DisableHWIntrinsic);
    }

    [Fact]
    public void PaethAvx2()
    {
        static void RunTest()
        {
            TestData data = new(PngFilterMethod.Paeth, Size);
            data.TestFilter();
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll);
    }

    [Fact]
    public void PaethVector()
    {
        static void RunTest()
        {
            TestData data = new(PngFilterMethod.Paeth, Size);
            data.TestFilter();
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2);
    }

    [Fact]
    public void Up()
    {
        static void RunTest()
        {
            TestData data = new(PngFilterMethod.Up, Size);
            data.TestFilter();
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.DisableHWIntrinsic);
    }

    [Fact]
    public void UpAvx2()
    {
        static void RunTest()
        {
            TestData data = new(PngFilterMethod.Up, Size);
            data.TestFilter();
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll);
    }

    [Fact]
    public void UpVector()
    {
        static void RunTest()
        {
            TestData data = new(PngFilterMethod.Up, Size);
            data.TestFilter();
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2);
    }

    [Fact]
    public void Sub()
    {
        static void RunTest()
        {
            TestData data = new(PngFilterMethod.Sub, Size);
            data.TestFilter();
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.DisableHWIntrinsic);
    }

    [Fact]
    public void SubAvx2()
    {
        static void RunTest()
        {
            TestData data = new(PngFilterMethod.Sub, Size);
            data.TestFilter();
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll);
    }

    [Fact]
    public void SubVector()
    {
        static void RunTest()
        {
            TestData data = new(PngFilterMethod.Sub, Size);
            data.TestFilter();
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2);
    }

    /// <summary>
    /// Verifies every encoder across pixel strides, register boundaries, and hardware widths.
    /// </summary>
    [Fact]
    public void EncodeMatchesReferencesAcrossRegisterBoundaries()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(
            AssertEncodersMatchReferences,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX2 | HwIntrinsics.DisableHWIntrinsic);

    /// <summary>
    /// Compares every filter with its independent scalar reference across SIMD boundaries and pixel strides.
    /// </summary>
    private static void AssertEncodersMatchReferences()
    {
        int[] bytesPerPixels = [1, 2, 3, 4, 6, 8];
        int[] lengths = [8, 9, 15, 16, 17, 31, 32, 33, 63, 64, 65, 127, 128, 129, 131, 132, 133, 135, 136, 137, 257];

        foreach (int bytesPerPixel in bytesPerPixels)
        {
            foreach (int length in lengths)
            {
                if (length < bytesPerPixel)
                {
                    continue;
                }

                byte[] scanline = new byte[length];
                byte[] previousScanline = new byte[length];
                Random random = new((bytesPerPixel * 397) + length);
                random.NextBytes(scanline);
                random.NextBytes(previousScanline);

                AssertFilterMatchesReference(PngFilterMethod.Sub, scanline, previousScanline, bytesPerPixel);

                AssertFilterMatchesReference(PngFilterMethod.Up, scanline, previousScanline, bytesPerPixel);

                AssertFilterMatchesReference(PngFilterMethod.Average, scanline, previousScanline, bytesPerPixel);

                AssertFilterMatchesReference(PngFilterMethod.Paeth, scanline, previousScanline, bytesPerPixel);
            }
        }
    }

    /// <summary>
    /// Compares one filter result and variance sum with its scalar reference.
    /// </summary>
    /// <param name="filter">The filter to evaluate.</param>
    /// <param name="scanline">The current scanline.</param>
    /// <param name="previousScanline">The preceding scanline.</param>
    /// <param name="bytesPerPixel">The component distance between adjacent pixels.</param>
    private static void AssertFilterMatchesReference(PngFilterMethod filter, byte[] scanline, byte[] previousScanline, int bytesPerPixel)
    {
        byte[] expected = new byte[scanline.Length + 1];
        byte[] actual = new byte[scanline.Length + 1];
        int expectedSum;
        int actualSum;

        switch (filter)
        {
            case PngFilterMethod.Sub:
                ReferenceImplementations.EncodeSubFilter(scanline, expected, bytesPerPixel, out expectedSum);
                PngFilterEncoder.Encode<SubFilterOperator>(scanline, scanline, actual, bytesPerPixel, out actualSum);
                break;

            case PngFilterMethod.Up:
                ReferenceImplementations.EncodeUpFilter(scanline, previousScanline, expected, out expectedSum);
                PngFilterEncoder.Encode<UpFilterOperator>(scanline, previousScanline, actual, 0, out actualSum);
                break;

            case PngFilterMethod.Average:
                ReferenceImplementations.EncodeAverageFilter(scanline, previousScanline, expected, bytesPerPixel, out expectedSum);

                PngFilterEncoder.Encode<AverageFilterOperator>(scanline, previousScanline, actual, bytesPerPixel, out actualSum);

                break;

            case PngFilterMethod.Paeth:
                ReferenceImplementations.EncodePaethFilter(scanline, previousScanline, expected, bytesPerPixel, out expectedSum);

                PngFilterEncoder.Encode<PaethFilterOperator>(scanline, previousScanline, actual, bytesPerPixel, out actualSum);

                break;

            default:
                throw new InvalidOperationException();
        }

        Assert.Equal(expectedSum, actualSum);
        Assert.Equal(expected, actual);
    }

    public class TestData
    {
        private readonly PngFilterMethod filter;
        private readonly int bpp;
        private readonly byte[] previousScanline;
        private readonly byte[] scanline;
        private readonly byte[] expectedResult;
        private readonly int expectedSum;
        private readonly byte[] resultBuffer;

        public TestData(PngFilterMethod filter, int size, int bpp = 4)
        {
            this.filter = filter;
            this.bpp = bpp;
            this.previousScanline = new byte[size * size * bpp];
            this.scanline = new byte[size * size * bpp];
            this.expectedResult = new byte[1 + (size * size * bpp)];
            this.resultBuffer = new byte[1 + (size * size * bpp)];

            Random rng = new(12345678);
            byte[] tmp = new byte[6];
            for (int i = 0; i < this.previousScanline.Length; i += bpp)
            {
                rng.NextBytes(tmp);

                this.previousScanline[i + 0] = tmp[0];
                this.previousScanline[i + 1] = tmp[1];
                this.previousScanline[i + 2] = tmp[2];
                this.previousScanline[i + 3] = 255;

                this.scanline[i + 0] = tmp[3];
                this.scanline[i + 1] = tmp[4];
                this.scanline[i + 2] = tmp[5];
                this.scanline[i + 3] = 255;
            }

            switch (this.filter)
            {
                case PngFilterMethod.Sub:
                    ReferenceImplementations.EncodeSubFilter(
                        this.scanline, this.expectedResult, this.bpp, out this.expectedSum);
                    break;

                case PngFilterMethod.Up:
                    ReferenceImplementations.EncodeUpFilter(
                        this.previousScanline, this.scanline, this.expectedResult, out this.expectedSum);
                    break;

                case PngFilterMethod.Average:
                    ReferenceImplementations.EncodeAverageFilter(
                        this.previousScanline, this.scanline, this.expectedResult, this.bpp, out this.expectedSum);
                    break;

                case PngFilterMethod.Paeth:
                    ReferenceImplementations.EncodePaethFilter(
                        this.previousScanline, this.scanline, this.expectedResult, this.bpp, out this.expectedSum);
                    break;

                case PngFilterMethod.None:
                case PngFilterMethod.Adaptive:
                default:
                    throw new InvalidOperationException();
            }
        }

        public void TestFilter()
        {
            int sum;
            switch (this.filter)
            {
                case PngFilterMethod.Sub:
                    PngFilterEncoder.Encode<SubFilterOperator>(this.scanline, this.scanline, this.resultBuffer, this.bpp, out sum);
                    break;

                case PngFilterMethod.Up:
                    PngFilterEncoder.Encode<UpFilterOperator>(this.previousScanline, this.scanline, this.resultBuffer, 0, out sum);
                    break;

                case PngFilterMethod.Average:
                    PngFilterEncoder.Encode<AverageFilterOperator>(this.previousScanline, this.scanline, this.resultBuffer, this.bpp, out sum);
                    break;

                case PngFilterMethod.Paeth:
                    PngFilterEncoder.Encode<PaethFilterOperator>(this.previousScanline, this.scanline, this.resultBuffer, this.bpp, out sum);
                    break;

                case PngFilterMethod.None:
                case PngFilterMethod.Adaptive:
                default:
                    throw new InvalidOperationException();
            }

            Assert.Equal(this.expectedSum, sum);
            Assert.Equal(this.expectedResult, this.resultBuffer);
        }
    }
}
