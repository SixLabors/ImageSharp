// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;
using System;
using System.IO;
using SixLabors.ImageSharp.Tests;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using SixLabors.ImageSharp.Processing;
using SDImage = System.Drawing.Image;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg
{
    [Config(typeof(Config.ShortClr))]
    public class LoadResizeSave : BenchmarkBase
    {
        private readonly Configuration configuration = new Configuration(new JpegConfigurationModule());

        private byte[] sourceBytes;

        private byte[] destBytes;

        private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

        [Params(
            TestImages.Jpeg.Baseline.Jpeg420Exif
            //, TestImages.Jpeg.Baseline.Calliphora
            )]
        public string TestImage { get; set; }

        [Params(false, true)]
        public bool EnableParallelExecution { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.configuration.MaxDegreeOfParallelism =
                this.EnableParallelExecution ? Environment.ProcessorCount : 1;

            if (this.sourceBytes == null)
            {
                this.sourceBytes = File.ReadAllBytes(this.TestImageFullPath);
            }

            if (this.destBytes == null)
            {
                this.destBytes = new byte[this.sourceBytes.Length];
            }
        }

        [Benchmark(Baseline = true)]
        public void SystemDrawing()
        {
            using (var sourceStream = new MemoryStream(this.sourceBytes))
            using (var destStream = new MemoryStream(this.destBytes))
            using (var source = SDImage.FromStream(sourceStream))
            using (var destination = new Bitmap(source.Width / 4, source.Height / 4))
            {
                using (var graphics = Graphics.FromImage(destination))
                {
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.DrawImage(source, 0, 0, 400, 400);
                }

                destination.Save(destStream, ImageFormat.Jpeg);
            }
        }

        [Benchmark]
        public void ImageSharp()
        {
            var source = Image.Load(this.configuration, this.sourceBytes, new JpegDecoder { IgnoreMetadata = true });
            using (source)
            using (var destStream = new MemoryStream(this.destBytes))
            {
                source.Mutate(c => c.Resize(source.Width / 4, source.Height / 4));
                source.SaveAsJpeg(destStream);
            }
        }
    }
}