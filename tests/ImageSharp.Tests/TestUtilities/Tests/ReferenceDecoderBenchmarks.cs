// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.TestUtilities.ReferenceCodecs;

using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.TestUtilities.Tests
{
    public class ReferenceDecoderBenchmarks
    {
        private ITestOutputHelper Output { get; }

        public const string SkipBenchmarks =
#if true
            "Benchmark, enable manually!";
#else
            null;
#endif

        public const int DefaultExecutionCount = 50;

        public static readonly string[] PngBenchmarkFiles =
            {
                TestImages.Png.CalliphoraPartial,
                TestImages.Png.Kaboom,
                TestImages.Png.Bike,
                TestImages.Png.Splash,
                TestImages.Png.SplashInterlaced
            };

        public static readonly string[] BmpBenchmarkFiles =
            {
                TestImages.Bmp.NegHeight,
                TestImages.Bmp.Car,
                TestImages.Bmp.V5Header
            };

        public ReferenceDecoderBenchmarks(ITestOutputHelper output)
        {
            this.Output = output;
        }

        [Theory(Skip = SkipBenchmarks)]
        [WithFile(TestImages.Png.Kaboom, PixelTypes.Rgba32)]
        public void BenchmarkMagickPngDecoder<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            this.BenchmarkDecoderImpl(PngBenchmarkFiles, new MagickReferenceDecoder(), "Magick Decode Png");
        }

        [Theory(Skip = SkipBenchmarks)]
        [WithFile(TestImages.Png.Kaboom, PixelTypes.Rgba32)]
        public void BenchmarkSystemDrawingPngDecoder<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            this.BenchmarkDecoderImpl(PngBenchmarkFiles, new SystemDrawingReferenceDecoder(), "System.Drawing Decode Png");
        }

        [Theory(Skip = SkipBenchmarks)]
        [WithFile(TestImages.Png.Kaboom, PixelTypes.Rgba32)]
        public void BenchmarkMagickBmpDecoder<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            this.BenchmarkDecoderImpl(BmpBenchmarkFiles, new MagickReferenceDecoder(), "Magick Decode Bmp");
        }

        [Theory(Skip = SkipBenchmarks)]
        [WithFile(TestImages.Png.Kaboom, PixelTypes.Rgba32)]
        public void BenchmarkSystemDrawingBmpDecoder<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            this.BenchmarkDecoderImpl(BmpBenchmarkFiles, new SystemDrawingReferenceDecoder(), "System.Drawing Decode Bmp");
        }

        private void BenchmarkDecoderImpl(IEnumerable<string> testFiles, IImageDecoder decoder, string info, int times = DefaultExecutionCount)
        {
            var measure = new MeasureFixture(this.Output);
            measure.Measure(
                times,
                () =>
                    {
                        foreach (string testFile in testFiles)
                        {
                            Image<Rgba32> image = TestFile.Create(testFile).CreateRgba32Image(decoder);
                            image.Dispose();
                        }
                    },
                info);
        }
    }
}
