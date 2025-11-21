// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

// Uncomment this to turn unit tests into benchmarks:
// #define BENCHMARKING
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Png.Filters;
using SixLabors.ImageSharp.Tests.TestUtilities;
using Xunit.Abstractions;

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

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX2);
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
                    SubFilter.Encode(this.scanline, this.resultBuffer, this.bpp, out sum);
                    break;

                case PngFilterMethod.Up:
                    UpFilter.Encode(this.previousScanline, this.scanline, this.resultBuffer, out sum);
                    break;

                case PngFilterMethod.Average:
                    AverageFilter.Encode(this.previousScanline, this.scanline, this.resultBuffer, (uint)this.bpp, out sum);
                    break;

                case PngFilterMethod.Paeth:
                    PaethFilter.Encode(this.previousScanline, this.scanline, this.resultBuffer, this.bpp, out sum);
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
