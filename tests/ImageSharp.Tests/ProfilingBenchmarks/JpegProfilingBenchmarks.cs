// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.IO;
using System.Linq;
using System.Numerics;

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

using Xunit;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests.ProfilingBenchmarks
{
    public class JpegProfilingBenchmarks : MeasureFixture
    {
        public JpegProfilingBenchmarks(ITestOutputHelper output)
            : base(output)
        {
        }

        public static readonly TheoryData<string> DecodeJpegData = new TheoryData<string>
        {
            TestImages.Jpeg.BenchmarkSuite.Jpeg400_SmallMonochrome,
            TestImages.Jpeg.BenchmarkSuite.Jpeg420Exif_MidSizeYCbCr,
            TestImages.Jpeg.BenchmarkSuite.Lake_Small444YCbCr,
            TestImages.Jpeg.BenchmarkSuite.MissingFF00ProgressiveBedroom159_MidSize420YCbCr,
            TestImages.Jpeg.BenchmarkSuite.BadRstProgressive518_Large444YCbCr,
            TestImages.Jpeg.BenchmarkSuite.ExifGetString750Transform_Huge420YCbCr,
        };

        [Theory(Skip = ProfilingSetup.SkipProfilingTests)]
        [MemberData(nameof(DecodeJpegData))]
        public void DecodeJpeg(string fileName)
        {
            this.DecodeJpegBenchmarkImpl(fileName, new JpegDecoder());
        }

        private void DecodeJpegBenchmarkImpl(string fileName, IImageDecoder decoder)
        {
            // do not run this on CI even by accident
            if (TestEnvironment.RunsOnCI)
            {
                return;
            }

            const int ExecutionCount = 20;

            if (!Vector.IsHardwareAccelerated)
            {
                throw new Exception("Vector.IsHardwareAccelerated == false! ('prefer32 bit' enabled?)");
            }

            string path = TestFile.GetInputFileFullPath(fileName);
            byte[] bytes = File.ReadAllBytes(path);

            this.Measure(
                ExecutionCount,
                () =>
                    {
                        var img = Image.Load<Rgba32>(bytes, decoder);
                        img.Dispose();
                    },
                // ReSharper disable once ExplicitCallerInfoArgument
                $"Decode {fileName}");
        }

        // Benchmark, enable manually!
        [Theory(Skip = ProfilingSetup.SkipProfilingTests)]
        [InlineData(1, 75, JpegSubsample.Ratio420)]
        [InlineData(30, 75, JpegSubsample.Ratio420)]
        [InlineData(30, 75, JpegSubsample.Ratio444)]
        [InlineData(30, 100, JpegSubsample.Ratio444)]
        public void EncodeJpeg(int executionCount, int quality, JpegSubsample subsample)
        {
            // do not run this on CI even by accident
            if (TestEnvironment.RunsOnCI)
            {
                return;
            }

            string[] testFiles = TestImages.Bmp.Benchmark
                .Concat(new[] { TestImages.Jpeg.Baseline.Calliphora, TestImages.Jpeg.Baseline.Cmyk }).ToArray();

            Image<Rgba32>[] testImages = testFiles.Select(
                tf => TestImageProvider<Rgba32>.File(tf, pixelTypeOverride: PixelTypes.Rgba32).GetImage()).ToArray();

            using (var ms = new MemoryStream())
            {
                this.Measure(
                    executionCount,
                    () =>
                        {
                            foreach (Image<Rgba32> img in testImages)
                            {
                                var options = new JpegEncoder { Quality = quality, Subsample = subsample };
                                img.Save(ms, options);
                                ms.Seek(0, SeekOrigin.Begin);
                            }
                        },
                    // ReSharper disable once ExplicitCallerInfoArgument
                    $@"Encode {testFiles.Length} images");
            }

            foreach (Image<Rgba32> image in testImages)
            {
                image.Dispose();
            }
        }
    }
}