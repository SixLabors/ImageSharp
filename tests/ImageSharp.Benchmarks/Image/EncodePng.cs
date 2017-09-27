// <copyright file="EncodePng.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace SixLabors.ImageSharp.Benchmarks.Image
{
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;

    using BenchmarkDotNet.Attributes;
    using SixLabors.ImageSharp.Formats.Png;
    using SixLabors.ImageSharp.Quantizers;
    using SixLabors.ImageSharp.Quantizers.Base;
    using SixLabors.ImageSharp.Tests;

    using CoreImage = ImageSharp.Image;

    public class EncodePng : BenchmarkBase
    {
        // System.Drawing needs this.
        private Stream bmpStream;
        private Image bmpDrawing;
        private Image<Rgba32> bmpCore;

        [Params(false)]
        public bool LargeImage { get; set; }

        [Params(false)]
        public bool UseOctreeQuantizer { get; set; }

        [GlobalSetup]
        public void ReadImages()
        {
            if (this.bmpStream == null)
            {
                string path = Path.Combine(
                    TestEnvironment.InputImagesDirectoryFullPath,
                    this.LargeImage ? TestImages.Jpeg.Baseline.Jpeg420Exif : TestImages.Bmp.Car);


                this.bmpStream = File.OpenRead(path);
                this.bmpCore = CoreImage.Load<Rgba32>(this.bmpStream);
                this.bmpStream.Position = 0;
                this.bmpDrawing = Image.FromStream(this.bmpStream);
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.bmpStream.Dispose();
            this.bmpCore.Dispose();
            this.bmpDrawing.Dispose();
        }

        [Benchmark(Baseline = true, Description = "System.Drawing Png")]
        public void PngSystemDrawing()
        {
            using (var memoryStream = new MemoryStream())
            {
                this.bmpDrawing.Save(memoryStream, ImageFormat.Png);
            }
        }

        [Benchmark(Description = "ImageSharp Png")]
        public void PngCore()
        {
            using (var memoryStream = new MemoryStream())
            {
                QuantizerBase<Rgba32> quantizer = this.UseOctreeQuantizer
                ? (QuantizerBase<Rgba32>)
                new OctreeQuantizer<Rgba32>()
                : new PaletteQuantizer<Rgba32>();

                var options = new PngEncoder { Quantizer = quantizer };
                this.bmpCore.SaveAsPng(memoryStream, options);
            }
        }
    }
}
