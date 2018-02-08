// <copyright file="DecodeBmp.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace SixLabors.ImageSharp.Benchmarks.Image
{
    using System.Drawing;
    using System.IO;

    using BenchmarkDotNet.Attributes;

    using CoreImage = ImageSharp.Image;

    using CoreSize = SixLabors.Primitives.Size;

    public class DecodeBmp : BenchmarkBase
    {
        private byte[] bmpBytes;

        [GlobalSetup]
        public void ReadImages()
        {
            if (this.bmpBytes == null)
            {
                this.bmpBytes = File.ReadAllBytes("../ImageSharp.Tests/TestImages/Formats/Bmp/Car.bmp");
            }
        }

        [Benchmark(Baseline = true, Description = "System.Drawing Bmp")]
        public Size BmpSystemDrawing()
        {
            using (MemoryStream memoryStream = new MemoryStream(this.bmpBytes))
            {
                using (Image image = Image.FromStream(memoryStream))
                {
                    return image.Size;
                }
            }
        }

        [Benchmark(Description = "ImageSharp Bmp")]
        public CoreSize BmpCore()
        {
            using (MemoryStream memoryStream = new MemoryStream(this.bmpBytes))
            {
                using (Image<Rgba32> image = CoreImage.Load<Rgba32>(memoryStream))
                {
                    return new CoreSize(image.Width, image.Height);
                }
            }
        }
    }
}
