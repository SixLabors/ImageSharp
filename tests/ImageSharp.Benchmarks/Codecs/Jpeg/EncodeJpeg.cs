// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Drawing.Imaging;
using System.IO;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;
using SDImage = System.Drawing.Image;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg
{
    public class EncodeJpeg
    {
        // System.Drawing needs this.
        private Stream bmpStream;
        private SDImage bmpDrawing;
        private Image<Rgba32> bmpCore;

        [GlobalSetup]
        public void ReadImages()
        {
            if (this.bmpStream == null)
            {
                const string TestImage = TestImages.Bmp.Car;
                this.bmpStream = File.OpenRead(Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, TestImage));
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

        [Benchmark(Baseline = true, Description = "System.Drawing Jpeg")]
        public void JpegSystemDrawing()
        {
            using var stream = new MemoryStream();
            this.bmpDrawing.Save(stream, ImageFormat.Jpeg);
        }

        [Benchmark(Description = "ImageSharp Jpeg")]
        public void JpegCore()
        {
            using var stream = new MemoryStream();
            this.bmpCore.SaveAsJpeg(stream);
        }
    }
}

/*
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.18363.959 (1909/November2018Update/19H2)
Intel Core i7-8650U CPU 1.90GHz (Kaby Lake R), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=3.1.302
  [Host]     : .NET Core 3.1.6 (CoreCLR 4.700.20.26901, CoreFX 4.700.20.31603), X64 RyuJIT
  DefaultJob : .NET Core 3.1.6 (CoreCLR 4.700.20.26901, CoreFX 4.700.20.31603), X64 RyuJIT


|                Method |     Mean |     Error |    StdDev | Ratio | RatioSD |
|---------------------- |---------:|----------:|----------:|------:|--------:|
| 'System.Drawing Jpeg' | 4.297 ms | 0.0244 ms | 0.0228 ms |  1.00 |    0.00 |
|     'ImageSharp Jpeg' | 5.286 ms | 0.1034 ms | 0.0967 ms |  1.23 |    0.02 |
*/
