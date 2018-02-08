// <copyright file="EncodeIndexedPng.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace SixLabors.ImageSharp.Benchmarks.Image
{
    using System.IO;

    using BenchmarkDotNet.Attributes;

    using SixLabors.ImageSharp;
    using SixLabors.ImageSharp.Formats;
    using SixLabors.ImageSharp.Formats.Png;
    using SixLabors.ImageSharp.Quantizers;

    using CoreImage = ImageSharp.Image;

    /// <summary>
    /// Benchmarks saving png files using different quantizers. System.Drawing cannot save indexed png files so we cannot compare.
    /// </summary>
    public class EncodeIndexedPng : BenchmarkBase
    {
        // System.Drawing needs this.
        private Stream bmpStream;
        private Image<Rgba32> bmpCore;

        [Params(false)]
        public bool LargeImage { get; set; }

        [GlobalSetup]
        public void ReadImages()
        {
            if (this.bmpStream == null)
            {
                string path = this.LargeImage
                                  ? "../ImageSharp.Tests/TestImages/Formats/Jpg/baseline/jpeg420exif.jpg"
                                  : "../ImageSharp.Tests/TestImages/Formats/Bmp/Car.bmp";
                this.bmpStream = File.OpenRead(path);
                this.bmpCore = CoreImage.Load<Rgba32>(this.bmpStream);
                this.bmpStream.Position = 0;
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.bmpStream.Dispose();
            this.bmpCore.Dispose();
        }

        [Benchmark(Baseline = true, Description = "ImageSharp Octree Png")]
        public void PngCoreOctree()
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                PngEncoder encoder = new PngEncoder() { Quantizer = new OctreeQuantizer<Rgba32>(), PaletteSize = 256 };

                this.bmpCore.SaveAsPng(memoryStream, encoder);
            }
        }

        [Benchmark(Description = "ImageSharp Octree NoDither Png")]
        public void PngCoreOctreeNoDIther()
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                PngEncoder options = new PngEncoder { Quantizer = new OctreeQuantizer<Rgba32> { Dither = false }, PaletteSize = 256 };

                this.bmpCore.SaveAsPng(memoryStream, options);
            }
        }

        [Benchmark(Description = "ImageSharp Palette Png")]
        public void PngCorePalette()
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                PngEncoder options = new PngEncoder { Quantizer = new PaletteQuantizer<Rgba32>(), PaletteSize = 256 };

                this.bmpCore.SaveAsPng(memoryStream, options);
            }
        }

        [Benchmark(Description = "ImageSharp Palette NoDither Png")]
        public void PngCorePaletteNoDither()
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                PngEncoder options = new PngEncoder { Quantizer = new PaletteQuantizer<Rgba32> { Dither = false }, PaletteSize = 256 };

                this.bmpCore.SaveAsPng(memoryStream, options);
            }
        }

        [Benchmark(Description = "ImageSharp Wu Png")]
        public void PngCoreWu()
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                PngEncoder options = new PngEncoder() { Quantizer = new WuQuantizer<Rgba32>(), PaletteSize = 256 };

                this.bmpCore.SaveAsPng(memoryStream, options);
            }
        }
    }
}