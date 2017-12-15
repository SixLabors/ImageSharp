// <copyright file="DecodePng.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace SixLabors.ImageSharp.Benchmarks.Image
{
    using System.Drawing;
    using System.IO;

    using BenchmarkDotNet.Attributes;

    using SixLabors.ImageSharp.Tests;

    using CoreImage = ImageSharp.Image;

    using CoreSize = SixLabors.Primitives.Size;

    [Config(typeof(Config.ShortClr))]
    public class DecodePng : BenchmarkBase
    {
        private byte[] pngBytes;

        private string TestImageFullPath => Path.Combine(
            TestEnvironment.InputImagesDirectoryFullPath,
            this.TestImage);

        [Params(TestImages.Png.Splash)]
        public string TestImage { get; set; }

        [GlobalSetup]
        public void ReadImages()
        {
            if (this.pngBytes == null)
            {
                this.pngBytes = File.ReadAllBytes(this.TestImageFullPath);
            }
        }

        [Benchmark(Baseline = true, Description = "System.Drawing Png")]
        public Size PngSystemDrawing()
        {
            using (var memoryStream = new MemoryStream(this.pngBytes))
            {
                using (var image = Image.FromStream(memoryStream))
                {
                    return image.Size;
                }
            }
        }

        [Benchmark(Description = "ImageSharp Png")]
        public CoreSize PngCore()
        {
            using (var memoryStream = new MemoryStream(this.pngBytes))
            {
                using (var image = CoreImage.Load<Rgba32>(memoryStream))
                {
                    return new CoreSize(image.Width, image.Height);
                }
            }
        }
    }
}
