// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests;
using SDImage = System.Drawing.Image;
using SDSize = System.Drawing.Size;

namespace SixLabors.ImageSharp.Benchmarks.Codecs
{
    [Config(typeof(Config.ShortMultiFramework))]
    public class DecodeBmp
    {
        private byte[] bmpBytes;

        private string TestImageFullPath
            => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

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
        public SDSize BmpSystemDrawing()
        {
            using var memoryStream = new MemoryStream(this.bmpBytes);
            using var image = SDImage.FromStream(memoryStream);
            return image.Size;
        }

        [Benchmark(Description = "ImageSharp Bmp")]
        public Size BmpImageSharp()
        {
            using var memoryStream = new MemoryStream(this.bmpBytes);
            using var image = Image.Load<Rgba32>(memoryStream);
            return new Size(image.Width, image.Height);
        }
    }
}
