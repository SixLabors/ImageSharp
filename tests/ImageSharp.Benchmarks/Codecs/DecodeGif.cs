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
    public class DecodeGif
    {
        private byte[] gifBytes;

        private string TestImageFullPath
            => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

        [GlobalSetup]
        public void ReadImages()
        {
            if (this.gifBytes == null)
            {
                this.gifBytes = File.ReadAllBytes(this.TestImageFullPath);
            }
        }

        [Params(TestImages.Gif.Rings)]
        public string TestImage { get; set; }

        [Benchmark(Baseline = true, Description = "System.Drawing Gif")]
        public SDSize GifSystemDrawing()
        {
            using var memoryStream = new MemoryStream(this.gifBytes);
            using var image = SDImage.FromStream(memoryStream);
            return image.Size;
        }

        [Benchmark(Description = "ImageSharp Gif")]
        public Size GifImageSharp()
        {
            using var memoryStream = new MemoryStream(this.gifBytes);
            using var image = Image.Load<Rgba32>(memoryStream);
            return new Size(image.Width, image.Height);
        }
    }
}
