// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Drawing.Imaging;
using System.IO;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;
using SDImage = System.Drawing.Image;

namespace SixLabors.ImageSharp.Benchmarks.Codecs
{
    [Config(typeof(Config.ShortMultiFramework))]
    public class EncodePng
    {
        // System.Drawing needs this.
        private Stream bmpStream;
        private SDImage bmpDrawing;
        private Image<Rgba32> bmpCore;

        [Params(false)]
        public bool LargeImage { get; set; }

        [GlobalSetup]
        public void ReadImages()
        {
            if (this.bmpStream == null)
            {
                string path = Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.LargeImage ? TestImages.Jpeg.Baseline.Jpeg420Exif : TestImages.Bmp.Car);
                this.bmpStream = File.OpenRead(path);
                this.bmpCore = Image.Load<Rgba32>(this.bmpStream);
                this.bmpStream.Position = 0;
                this.bmpDrawing = SDImage.FromStream(this.bmpStream);
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.bmpStream.Dispose();
            this.bmpStream = null;
            this.bmpCore.Dispose();
            this.bmpDrawing.Dispose();
        }

        [Benchmark(Baseline = true, Description = "System.Drawing Png")]
        public void PngSystemDrawing()
        {
            using var memoryStream = new MemoryStream();
            this.bmpDrawing.Save(memoryStream, ImageFormat.Png);
        }

        [Benchmark(Description = "ImageSharp Png")]
        public void PngCore()
        {
            using var memoryStream = new MemoryStream();
            var encoder = new PngEncoder { FilterMethod = PngFilterMethod.None };
            this.bmpCore.SaveAsPng(memoryStream, encoder);
        }
    }
}
