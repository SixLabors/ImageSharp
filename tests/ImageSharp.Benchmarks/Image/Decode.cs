// <copyright file="Decode.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Benchmarks.Image
{
    using System.Drawing;
    using System.IO;

    using BenchmarkDotNet.Attributes;
    using CoreSize = ImageSharp.Size;
    using CoreImage = ImageSharp.Image;

    public class Decode
    {
        private byte[] bmpBytes;
        private byte[] gifBytes;
        private byte[] jpegBytes;
        private byte[] pngBytes;

        [Setup]
        public void ReadImages()
        {
            if (bmpBytes == null)
            {
                bmpBytes = File.ReadAllBytes("../ImageSharp.Tests/TestImages/Formats/Bmp/Car.bmp");
            }
            if (gifBytes == null)
            {
                gifBytes = File.ReadAllBytes("../ImageSharp.Tests/TestImages/Formats/Gif/rings.gif");
            }
            if (jpegBytes == null)
            {
                jpegBytes = File.ReadAllBytes("../ImageSharp.Tests/TestImages/Formats/Jpg/Calliphora.jpg");
            }
            if (pngBytes == null)
            {
                pngBytes = File.ReadAllBytes("../ImageSharp.Tests/TestImages/Formats/Png/splash.png");
            }
        }

        [Benchmark(Description = "System.Drawing Bmp")]
        public Size BmpSystemDrawing()
        {
            using (MemoryStream memoryStream = new MemoryStream(bmpBytes))
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
            using (MemoryStream memoryStream = new MemoryStream(bmpBytes))
            {
                CoreImage image = new CoreImage(memoryStream);
                return new CoreSize(image.Width, image.Height);
            }
        }

        [Benchmark(Description = "System.Drawing Gif")]
        public Size GifSystemDrawing()
        {
            using (MemoryStream memoryStream = new MemoryStream(gifBytes))
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
            using (MemoryStream memoryStream = new MemoryStream(gifBytes))
            {
                CoreImage image = new CoreImage(memoryStream);
                return new CoreSize(image.Width, image.Height);
            }
        }

        [Benchmark(Description = "System.Drawing Jpeg")]
        public Size JpegSystemDrawing()
        {
            using (MemoryStream memoryStream = new MemoryStream(jpegBytes))
            {
                using (Image image = Image.FromStream(memoryStream))
                {
                    return image.Size;
                }
            }
        }

        [Benchmark(Description = "ImageSharp Jpeg")]
        public CoreSize JpegCore()
        {
            using (MemoryStream memoryStream = new MemoryStream(jpegBytes))
            {
                CoreImage image = new CoreImage(memoryStream);
                return new CoreSize(image.Width, image.Height);
            }
        }

        [Benchmark(Description = "System.Drawing Png")]
        public Size PngSystemDrawing()
        {
            using (MemoryStream memoryStream = new MemoryStream(pngBytes))
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
            using (MemoryStream memoryStream = new MemoryStream(pngBytes))
            {
                CoreImage image = new CoreImage(memoryStream);
                return new CoreSize(image.Width, image.Height);
            }
        }
    }
}
