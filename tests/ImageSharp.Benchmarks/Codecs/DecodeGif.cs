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
    public class DecodeGif : BenchmarkBase
    {
        private byte[] gifBytes;

        private string TestImageFullPath => Path.Combine(TestEnvironment.InputImagesDirectoryFullPath, this.TestImage);

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
        public Size GifSystemDrawing()
        {
            using (var memoryStream = new MemoryStream(this.gifBytes))
            {
                using (var image = SDImage.FromStream(memoryStream))
                {
                    return image.Size;
                }
            }
        }

        [Benchmark(Description = "ImageSharp Gif")]
        public CoreSize GifCore()
        {
            using (var memoryStream = new MemoryStream(this.gifBytes))
            {
                using (var image = Image.Load<Rgba32>(memoryStream))
                {
                    return new CoreSize(image.Width, image.Height);
                }
            }
        }
    }
}
