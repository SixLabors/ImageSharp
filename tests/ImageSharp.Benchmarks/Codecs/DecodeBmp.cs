// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Drawing;
using System.IO;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;
using CoreSize = SixLabors.Primitives.Size;
using SDImage = System.Drawing.Image;

namespace SixLabors.ImageSharp.Benchmarks.Codecs
{
    [Config(typeof(Config.ShortClr))]
    public class DecodeBmp : BenchmarkBase
    {
        private byte[] bmpBytes;

        private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

        [GlobalSetup]
        public void ReadImages()
        {
            if (this.bmpBytes == null)
            {
                this.bmpBytes = File.ReadAllBytes(this.TestImageFullPath);
            }
        }

        [Params(TestImages.Bmp.Car)]
        public string TestImage { get; set; }

        [Benchmark(Baseline = true, Description = "System.Drawing Bmp")]
        public Size BmpSystemDrawing()
        {
            using (var memoryStream = new MemoryStream(this.bmpBytes))
            {
                using (var image = SDImage.FromStream(memoryStream))
                {
                    return image.Size;
                }
            }
        }

        [Benchmark(Description = "ImageSharp Bmp")]
        public CoreSize BmpCore()
        {
            using (var memoryStream = new MemoryStream(this.bmpBytes))
            {
                using (var image = Image.Load<Rgba32>(memoryStream))
                {
                    return new CoreSize(image.Width, image.Height);
                }
            }
        }
    }
}
