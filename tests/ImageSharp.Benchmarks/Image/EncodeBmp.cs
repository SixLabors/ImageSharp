// <copyright file="EncodeBmp.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Benchmarks.Image
{
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;

    using BenchmarkDotNet.Attributes;
    using CoreImage = ImageSharp.Image;

    public class EncodeBmp
    {
        // System.Drawing needs this.
        private Stream bmpStream;
        private Image bmpDrawing;
        private CoreImage bmpCore;

        [Setup]
        public void ReadImages()
        {
            if (this.bmpStream == null)
            {
                this.bmpStream = File.OpenRead("../ImageSharp.Tests/TestImages/Formats/Bmp/Car.bmp");
                this.bmpCore = new CoreImage(this.bmpStream);
                this.bmpStream.Position = 0;
                this.bmpDrawing = Image.FromStream(this.bmpStream);
            }
        }

        [Benchmark(Baseline = true, Description = "System.Drawing Bmp")]
        public void BmpSystemDrawing()
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                this.bmpDrawing.Save(memoryStream, ImageFormat.Bmp);
            }
        }

        [Benchmark(Description = "ImageSharp Bmp")]
        public void BmpCore()
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                this.bmpCore.SaveAsBmp(memoryStream);
            }
        }
    }
}
