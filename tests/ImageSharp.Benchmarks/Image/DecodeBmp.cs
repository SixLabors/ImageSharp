// <copyright file="DecodeBmp.cs" company="James Jackson-South">
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

    public class DecodeBmp
    {
        private byte[] bmpBytes;

        [Setup]
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
                CoreImage image = new CoreImage(memoryStream);
                return new CoreSize(image.Width, image.Height);
            }
        }
    }
}
