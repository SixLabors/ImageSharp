// <copyright file="EncodeIndexedPng.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using System.IO;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Quantization;
using CoreImage = SixLabors.ImageSharp.Image;

namespace SixLabors.ImageSharp.Benchmarks.Image
{
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
            using (var memoryStream = new MemoryStream())
            {
                var encoder = new PngEncoder { Quantizer = new OctreeQuantizer() };

                this.bmpCore.SaveAsPng(memoryStream, encoder);
            }
        }

        [Benchmark(Description = "ImageSharp Octree NoDither Png")]
        public void PngCoreOctreeNoDither()
        {
            using (var memoryStream = new MemoryStream())
            {
                var options = new PngEncoder { Quantizer = new OctreeQuantizer(false) };

                this.bmpCore.SaveAsPng(memoryStream, options);
            }
        }

        [Benchmark(Description = "ImageSharp Palette Png")]
        public void PngCorePalette()
        {
            using (var memoryStream = new MemoryStream())
            {
                var options = new PngEncoder { Quantizer = new PaletteQuantizer() };

                this.bmpCore.SaveAsPng(memoryStream, options);
            }
        }

        [Benchmark(Description = "ImageSharp Palette NoDither Png")]
        public void PngCorePaletteNoDither()
        {
            using (var memoryStream = new MemoryStream())
            {
                var options = new PngEncoder { Quantizer = new PaletteQuantizer(false) };

                this.bmpCore.SaveAsPng(memoryStream, options);
            }
        }

        [Benchmark(Description = "ImageSharp Wu Png")]
        public void PngCoreWu()
        {
            using (var memoryStream = new MemoryStream())
            {
                var options = new PngEncoder { Quantizer = new WuQuantizer() };

                this.bmpCore.SaveAsPng(memoryStream, options);
            }
        }

        [Benchmark(Description = "ImageSharp Wu NoDither Png")]
        public void PngCoreWuNoDither()
        {
            using (var memoryStream = new MemoryStream())
            {
                var options = new PngEncoder { Quantizer = new WuQuantizer(false) };

                this.bmpCore.SaveAsPng(memoryStream, options);
            }
        }
    }
}