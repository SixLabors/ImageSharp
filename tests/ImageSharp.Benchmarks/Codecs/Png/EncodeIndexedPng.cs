// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Quantization;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks.Codecs
{
    /// <summary>
    /// Benchmarks saving png files using different quantizers.
    /// System.Drawing cannot save indexed png files so we cannot compare.
    /// </summary>
    [Config(typeof(Config.ShortMultiFramework))]
    public class EncodeIndexedPng
    {
        // System.Drawing needs this.
        private Stream bmpStream;
        private Image<Rgba32> bmpCore;

        [GlobalSetup]
        public void ReadImages()
        {
            if (this.bmpStream == null)
            {
                this.bmpStream = File.OpenRead(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImages.Bmp.Car));
                this.bmpCore = Image.Load<Rgba32>(this.bmpStream);
                this.bmpStream.Position = 0;
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.bmpStream.Dispose();
            this.bmpStream = null;
            this.bmpCore.Dispose();
        }

        [Benchmark(Baseline = true, Description = "ImageSharp Octree Png")]
        public void PngCoreOctree()
        {
            using var memoryStream = new MemoryStream();
            var options = new PngEncoder { Quantizer = KnownQuantizers.Octree };
            this.bmpCore.SaveAsPng(memoryStream, options);
        }

        [Benchmark(Description = "ImageSharp Octree NoDither Png")]
        public void PngCoreOctreeNoDither()
        {
            using var memoryStream = new MemoryStream();
            var options = new PngEncoder { Quantizer = new OctreeQuantizer(new QuantizerOptions { Dither = null }) };
            this.bmpCore.SaveAsPng(memoryStream, options);
        }

        [Benchmark(Description = "ImageSharp Palette Png")]
        public void PngCorePalette()
        {
            using var memoryStream = new MemoryStream();
            var options = new PngEncoder { Quantizer = KnownQuantizers.WebSafe };
            this.bmpCore.SaveAsPng(memoryStream, options);
        }

        [Benchmark(Description = "ImageSharp Palette NoDither Png")]
        public void PngCorePaletteNoDither()
        {
            using var memoryStream = new MemoryStream();
            var options = new PngEncoder { Quantizer = new WebSafePaletteQuantizer(new QuantizerOptions { Dither = null }) };
            this.bmpCore.SaveAsPng(memoryStream, options);
        }

        [Benchmark(Description = "ImageSharp Wu Png")]
        public void PngCoreWu()
        {
            using var memoryStream = new MemoryStream();
            var options = new PngEncoder { Quantizer = KnownQuantizers.Wu };
            this.bmpCore.SaveAsPng(memoryStream, options);
        }

        [Benchmark(Description = "ImageSharp Wu NoDither Png")]
        public void PngCoreWuNoDither()
        {
            using var memoryStream = new MemoryStream();
            var options = new PngEncoder { Quantizer = new WuQuantizer(new QuantizerOptions { Dither = null }) };
            this.bmpCore.SaveAsPng(memoryStream, options);
        }
    }
}
