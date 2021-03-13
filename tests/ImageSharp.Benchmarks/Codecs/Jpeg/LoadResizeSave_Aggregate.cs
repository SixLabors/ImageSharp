// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg
{
    [Config(typeof(Config.ShortMultiFramework))]
    public class LoadResizeSave_Aggregate : MultiImageBenchmarkBase
    {
        protected override IEnumerable<string> InputImageSubfoldersOrFiles =>
            new[]
                {
                    TestImages.Jpeg.BenchmarkSuite.Jpeg400_SmallMonochrome,
                    TestImages.Jpeg.BenchmarkSuite.Jpeg420Exif_MidSizeYCbCr,
                    TestImages.Jpeg.BenchmarkSuite.Lake_Small444YCbCr,
                    TestImages.Jpeg.BenchmarkSuite.MissingFF00ProgressiveBedroom159_MidSize420YCbCr,
                    TestImages.Jpeg.BenchmarkSuite.ExifGetString750Transform_Huge420YCbCr,
                };

        [Params(InputImageCategory.AllImages)]
        public override InputImageCategory InputCategory { get; set; }

        private readonly Configuration configuration = new Configuration(new JpegConfigurationModule());

        private byte[] destBytes;

        public override void Setup()
        {
            base.Setup();

            this.configuration.MaxDegreeOfParallelism = 1;
            const int MaxOutputSizeInBytes = 2 * 1024 * 1024; // ~2 MB
            this.destBytes = new byte[MaxOutputSizeInBytes];
        }

        [Benchmark(Baseline = true)]
        public void SystemDrawing()
            => this.ForEachStream(
                sourceStream =>
                {
                    using (var destStream = new MemoryStream(this.destBytes))
                    using (var source = System.Drawing.Image.FromStream(sourceStream))
                    using (var destination = new Bitmap(source.Width / 4, source.Height / 4))
                    {
                        using (var g = Graphics.FromImage(destination))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.CompositingQuality = CompositingQuality.HighQuality;
                            g.DrawImage(source, 0, 0, 400, 400);
                        }

                        destination.Save(destStream, ImageFormat.Jpeg);
                    }

                    return null;
                });

        [Benchmark]
        public void ImageSharp()
            => this.ForEachStream(
                sourceStream =>
                {
                    using (var source = Image.Load<Rgba32>(
                        this.configuration,
                        sourceStream,
                        new JpegDecoder { IgnoreMetadata = true }))
                    {
                        using var destStream = new MemoryStream(this.destBytes);
                        source.Mutate(c => c.Resize(source.Width / 4, source.Height / 4));
                        source.SaveAsJpeg(destStream);
                    }

                    return null;
                });
    }
}
