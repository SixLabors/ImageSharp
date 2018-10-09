// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;

using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Benchmarks
{
    [Config(typeof(Config.ShortClr))]
    public abstract class ResizeBenchmarkBase
    {
        protected readonly Configuration Configuration = new Configuration(new JpegConfigurationModule());

        private Image<Rgba32> sourceImage;

        private Bitmap sourceBitmap;

        public const int SourceSize = 3032;

        public const int DestSize = 400;

        [GlobalSetup]
        public void Setup()
        {
            this.sourceImage = new Image<Rgba32>(this.Configuration, SourceSize, SourceSize);
            this.sourceBitmap = new Bitmap(SourceSize, SourceSize);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.sourceImage.Dispose();
            this.sourceBitmap.Dispose();
        }

        [Benchmark(Baseline = true)]
        public int SystemDrawing()
        {
            using (var destination = new Bitmap(DestSize, DestSize))
            {
                using (var graphics = Graphics.FromImage(destination))
                {
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.DrawImage(this.sourceBitmap, 0, 0, DestSize, DestSize);
                }

                return destination.Width;
            }
        }

        [Benchmark(Description = "ImageSharp, MaxDegreeOfParallelism = 1")]
        public int ImageSharp_P1() => this.RunImageSharpResize(1);

        [Benchmark(Description = "ImageSharp, MaxDegreeOfParallelism = 4")]
        public int ImageSharp_P4() => this.RunImageSharpResize(4);

        [Benchmark(Description = "ImageSharp, MaxDegreeOfParallelism = 8")]
        public int ImageSharp_P8() => this.RunImageSharpResize(8);

        protected int RunImageSharpResize(int maxDegreeOfParallelism)
        {
            this.Configuration.MaxDegreeOfParallelism = maxDegreeOfParallelism;

            using (Image<Rgba32> clone = this.sourceImage.Clone(this.ExecuteResizeOperation))
            {
                return clone.Width;
            }
        }

        protected abstract void ExecuteResizeOperation(IImageProcessingContext<Rgba32> ctx);
    }
    
    public class Resize_Bicubic : ResizeBenchmarkBase
    {
        protected override void ExecuteResizeOperation(IImageProcessingContext<Rgba32> ctx)
        {
            ctx.Resize(DestSize, DestSize, KnownResamplers.Bicubic);
        }
    }

    public class Resize_BicubicCompand : ResizeBenchmarkBase
    {
        protected override void ExecuteResizeOperation(IImageProcessingContext<Rgba32> ctx)
        {
            ctx.Resize(DestSize, DestSize, KnownResamplers.Bicubic, true);
        }
    }
}
