// <copyright file="Encode.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Benchmarks.Image
{
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;

    using BenchmarkDotNet.Attributes;
    using CoreImage = ImageProcessorCore.Image;

    public class Encode
    {
        // System.Drawing needs this.
        private Stream bmpStream;
        private Stream gifStream;
        private Stream jpegStream;
        private Stream pngStream;

        private Image bmpDrawing;
        private Image gifDrawing;
        private Image jpegDrawing;
        private Image pngDrawing;

        private CoreImage bmpCore;
        private CoreImage gifCore;
        private CoreImage jpegCore;
        private CoreImage pngCore;

        [Setup]
        public void ReadImages()
        {
            if (bmpStream == null)
            { 
                bmpStream = File.OpenRead("../ImageProcessorCore.Tests/TestImages/Formats/Bmp/Car.bmp");
                bmpCore = new CoreImage(bmpStream);
                bmpStream.Position = 0;
                bmpDrawing = Image.FromStream(bmpStream);
            }

            if (gifStream == null)
            { 
                gifStream = File.OpenRead("../ImageProcessorCore.Tests/TestImages/Formats/Gif/rings.gif");
                gifCore = new CoreImage(gifStream);
                gifStream.Position = 0;
                gifDrawing = Image.FromStream(gifStream);
            }

            if (jpegStream == null)
            { 
                jpegStream = File.OpenRead("../ImageProcessorCore.Tests/TestImages/Formats/Jpg/Calliphora.jpg");
                jpegCore = new CoreImage(jpegStream);
                jpegStream.Position = 0;
                jpegDrawing = Image.FromStream(jpegStream);
            }

            if (pngStream == null)
            { 
                pngStream = File.OpenRead("../ImageProcessorCore.Tests/TestImages/Formats/Png/splash.png");
                pngCore = new CoreImage(pngStream);
                pngStream.Position = 0;
                pngDrawing = Image.FromStream(pngStream);
            }
        }

        [Benchmark(Description = "System.Drawing Bmp")]
        public void BmpSystemDrawing()
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                bmpDrawing.Save(memoryStream, ImageFormat.Bmp);
            }
        }

        [Benchmark(Description = "ImageProcessorCore Bmp")]
        public void BmpCore()
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                bmpCore.SaveAsBmp(memoryStream);
            }
        }

        [Benchmark(Description = "System.Drawing Gif")]
        public void GifSystemDrawing()
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                bmpDrawing.Save(memoryStream, ImageFormat.Gif);
            }
        }

        [Benchmark(Description = "ImageProcessorCore Gif")]
        public void GifCore()
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                bmpCore.SaveAsGif(memoryStream);
            }
        }

        [Benchmark(Description = "System.Drawing Jpeg")]
        public void JpegSystemDrawing()
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                bmpDrawing.Save(memoryStream, ImageFormat.Jpeg);
            }
        }

        [Benchmark(Description = "ImageProcessorCore Jpeg")]
        public void JpegCore()
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                bmpCore.SaveAsJpeg(memoryStream);
            }
        }

        [Benchmark(Description = "System.Drawing Png")]
        public void PngSystemDrawing()
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                bmpDrawing.Save(memoryStream, ImageFormat.Png);
            }
        }

        [Benchmark(Description = "ImageProcessorCore Png")]
        public void PngCore()
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                bmpCore.SaveAsPng(memoryStream);
            }
        }
    }
}
