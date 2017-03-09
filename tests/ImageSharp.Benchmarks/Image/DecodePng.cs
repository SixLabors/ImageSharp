// <copyright file="DecodePng.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Benchmarks.Image
{
    using System.Drawing;
    using System.IO;

    using BenchmarkDotNet.Attributes;

    using CoreImage = ImageSharp.Image;
    using CoreSize = ImageSharp.Size;

    public class DecodePng : BenchmarkBase
    {
        private byte[] pngBytes;

        [Setup]
        public void ReadImages()
        {
            if (this.pngBytes == null)
            {
                this.pngBytes = File.ReadAllBytes("../ImageSharp.Tests/TestImages/Formats/Png/splash.png");
            }
        }

        [Benchmark(Baseline = true, Description = "System.Drawing Png")]
        public Size PngSystemDrawing()
        {
            using (MemoryStream memoryStream = new MemoryStream(this.pngBytes))
            {
                using (Image image = Image.FromStream(memoryStream))
                {
                    return image.Size;
                }
            }
        }

        [Benchmark(Description = "ImageSharp Png")]
        public CoreSize PngCore()
        {
            using (MemoryStream memoryStream = new MemoryStream(this.pngBytes))
            {
                using (CoreImage image = new CoreImage(memoryStream))
                {
                    return new CoreSize(image.Width, image.Height);
                }
            }
        }
    }
}
