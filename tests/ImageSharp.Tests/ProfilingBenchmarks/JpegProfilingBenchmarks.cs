// Copyright (c) Six Labors.
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

// in this file, comments are used for disabling stuff for local execution
#pragma warning disable SA1515

namespace SixLabors.ImageSharp.Tests.ProfilingBenchmarks
{
    public class JpegProfilingBenchmarks : MeasureFixture
    {
        public JpegProfilingBenchmarks(ITestOutputHelper output)
            : base(output)
        {
        }

        public static readonly TheoryData<string, int> DecodeJpegData = new TheoryData<string, int>
        {
            { TestImages.Jpeg.BenchmarkSuite.Jpeg400_SmallMonochrome, 20 },
            { TestImages.Jpeg.BenchmarkSuite.Jpeg420Exif_MidSizeYCbCr, 20 },
            { TestImages.Jpeg.BenchmarkSuite.Lake_Small444YCbCr, 40 },
            // { TestImages.Jpeg.BenchmarkSuite.MissingFF00ProgressiveBedroom159_MidSize420YCbCr, 10 },
            // { TestImages.Jpeg.BenchmarkSuite.BadRstProgressive518_Large444YCbCr, 5 },
            { TestImages.Jpeg.BenchmarkSuite.ExifGetString750Transform_Huge420YCbCr, 5 }
        };

        [Theory(Skip = ProfilingSetup.SkipProfilingTests)]
        [MemberData(nameof(DecodeJpegData))]
        public void DecodeJpeg(string fileName, int executionCount)
        {
            var decoder = new JpegDecoder()
            {
                IgnoreMetadata = true
            };
            this.DecodeJpegBenchmarkImpl(fileName, decoder, executionCount);
        }

        private void DecodeJpegBenchmarkImpl(string fileName, IImageDecoder decoder, int executionCount)
        {
            // do not run this on CI even by accident
            if (TestEnvironment.RunsOnCI)
            {
                return;
            }

            if (!Vector.IsHardwareAccelerated)
            {
                throw new Exception("Vector.IsHardwareAccelerated == false! ('prefer32 bit' enabled?)");
            }

            string path = TestFile.GetInputFileFullPath(fileName);
            byte[] bytes = File.ReadAllBytes(path);

            this.Measure(
                executionCount,
                () =>
                    {
                        var img = Image.Load<Rgba32>(bytes, decoder);
                        img.Dispose();
                    },
#pragma warning disable SA1515 // Single-line comment should be preceded by blank line
                              // ReSharper disable once ExplicitCallerInfoArgument
                $"Decode {fileName}");
#pragma warning restore SA1515 // Single-line comment should be preceded by blank line
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
#pragma warning disable SA1515 // Single-line comment should be preceded by blank line
                              // ReSharper disable once ExplicitCallerInfoArgument
                    $@"Encode {testFiles.Length} images");
#pragma warning restore SA1515 // Single-line comment should be preceded by blank line
            }

            foreach (Image<Rgba32> image in testImages)
            {
                image.Dispose();
            }
        }
    }
}
