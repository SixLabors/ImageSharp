// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Drawing.Imaging;
using System.IO;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;
using SDImage = System.Drawing.Image;

namespace SixLabors.ImageSharp.Benchmarks.Codecs
{
    [Config(typeof(Config.ShortClr))]
    public class EncodeBmp : BenchmarkBase
    {
        private Stream bmpStream;
        private SDImage bmpDrawing;
        private Image<Rgba32> bmpCore;

        [GlobalSetup]
        public void ReadImages()
        {
            if (this.bmpStream == null)
            {
                this.bmpStream = File.OpenRead(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImages.Bmp.Car));
                this.bmpCore = Image.Load<Rgba32>(this.bmpStream);
                this.bmpStream.Position = 0;
                this.bmpDrawing = SDImage.FromStream(this.bmpStream);
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.bmpStream.Dispose();
            this.bmpCore.Dispose();
            this.bmpDrawing.Dispose();
        }

        [Benchmark(Baseline = true, Description = "System.Drawing Bmp")]
        public void BmpSystemDrawing()
        {
            using (var memoryStream = new MemoryStream())
            {
                this.bmpDrawing.Save(memoryStream, ImageFormat.Bmp);
            }
        }

        [Benchmark(Description = "ImageSharp Bmp")]
        public void BmpCore()
        {
            using (var memoryStream = new MemoryStream())
            {
                this.bmpCore.SaveAsBmp(memoryStream);
            }
        }
    }
}