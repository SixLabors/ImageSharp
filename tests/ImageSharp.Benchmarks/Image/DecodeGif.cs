// <copyright file="DecodeGif.cs" company="James Jackson-South">
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

    public class DecodeGif : BenchmarkBase
    {
        private byte[] gifBytes;

        [Setup]
        public void ReadImages()
        {
            if (this.gifBytes == null)
            {
                this.gifBytes = File.ReadAllBytes("../ImageSharp.Tests/TestImages/Formats/Gif/rings.gif");
            }
        }

        [Benchmark(Baseline = true, Description = "System.Drawing Gif")]
        public Size GifSystemDrawing()
        {
            using (MemoryStream memoryStream = new MemoryStream(this.gifBytes))
            {
                using (Image image = Image.FromStream(memoryStream))
                {
                    return image.Size;
                }
            }
        }

        [Benchmark(Description = "ImageSharp Gif")]
        public CoreSize GifCore()
        {
            using (MemoryStream memoryStream = new MemoryStream(this.gifBytes))
            {
                using (CoreImage image = new CoreImage(memoryStream))
                {
                    return new CoreSize(image.Width, image.Height);
                }
            }
        }
    }
}
