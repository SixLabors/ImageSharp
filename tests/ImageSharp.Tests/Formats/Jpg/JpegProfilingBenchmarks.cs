// <copyright file="JpegProfilingBenchmarks.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.IO;
    using System.Linq;

    using ImageSharp.Formats;

    using Xunit;
    using Xunit.Abstractions;

    public class JpegProfilingBenchmarks : MeasureFixture
    {
        public JpegProfilingBenchmarks(ITestOutputHelper output)
            : base(output)
        {
        }

        // [Theory] // Benchmark, enable manually
        [InlineData(30, TestImages.Jpeg.Baseline.Cmyk)]
        [InlineData(30, TestImages.Jpeg.Baseline.Ycck)]
        [InlineData(30, TestImages.Jpeg.Baseline.Calliphora)]
        [InlineData(30, TestImages.Jpeg.Baseline.Jpeg400)]
        [InlineData(30, TestImages.Jpeg.Baseline.Jpeg420)]
        [InlineData(30, TestImages.Jpeg.Baseline.Jpeg444)]
        public void DecodeJpeg(int executionCount, string fileName)
        {
            string path = TestFile.GetPath(fileName);
            byte[] bytes = File.ReadAllBytes(path);

            this.Measure(
                100,
                () =>
                    {
                        Image img = new Image(bytes);
                    },
                // ReSharper disable once ExplicitCallerInfoArgument
                $"Decode {fileName}");

        }

        // Benchmark, enable manually!
        // [Theory]
        [InlineData(1, 75, JpegSubsample.Ratio420)]
        [InlineData(30, 75, JpegSubsample.Ratio420)]
        [InlineData(30, 75, JpegSubsample.Ratio444)]
        [InlineData(30, 100, JpegSubsample.Ratio444)]
        public void EncodeJpeg(int executionCount, int quality, JpegSubsample subsample)
        {
            string[] testFiles = TestImages.Bmp.All
                .Concat(new[] { TestImages.Jpeg.Baseline.Calliphora, TestImages.Jpeg.Baseline.Cmyk })
                .ToArray();

            Image<Color>[] testImages =
                testFiles.Select(
                        tf => TestImageProvider<Color>.File(tf, pixelTypeOverride: PixelTypes.StandardImageClass).GetImage())
                    .ToArray();

            using (MemoryStream ms = new MemoryStream())
            {
                this.Measure(executionCount,
                    () =>
                    {
                        foreach (Image<Color> img in testImages)
                        {
                            JpegEncoder encoder = new JpegEncoder() { Quality = quality, Subsample = subsample };
                            img.Save(ms, encoder);
                            ms.Seek(0, SeekOrigin.Begin);
                        }
                    },
                    // ReSharper disable once ExplicitCallerInfoArgument
                    $@"Encode {testFiles.Length} images"
                    );
            }
        }

    }
}