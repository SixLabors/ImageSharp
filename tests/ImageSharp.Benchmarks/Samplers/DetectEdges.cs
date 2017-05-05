// <copyright file="DetectEdges.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Benchmarks
{
    using System.IO;

    using BenchmarkDotNet.Attributes;

    using Processing;

    using CoreImage = ImageSharp.Image;

    public class DetectEdges : BenchmarkBase
    {
        private Image<Rgba32> image;

        [Setup]
        public void ReadImage()
        {
            if (this.image == null)
            {
                using (FileStream stream = File.OpenRead("../ImageSharp.Tests/TestImages/Formats/Bmp/Car.bmp"))
                {
                    this.image = CoreImage.Load<Rgba32>(stream);
                }
            }
        }

        [Cleanup]
        public void Cleanup()
        {
            this.image.Dispose();
        }

        [Benchmark(Description = "ImageSharp DetectEdges")]
        public void ImageProcessorCoreDetectEdges()
        {
            this.image.DetectEdges(EdgeDetection.Kayyali);
            this.image.DetectEdges(EdgeDetection.Kayyali);
            this.image.DetectEdges(EdgeDetection.Kirsch);
            this.image.DetectEdges(EdgeDetection.Lapacian3X3);
            this.image.DetectEdges(EdgeDetection.Lapacian5X5);
            this.image.DetectEdges(EdgeDetection.LaplacianOfGaussian);
            this.image.DetectEdges(EdgeDetection.Prewitt);
            this.image.DetectEdges(EdgeDetection.RobertsCross);
            this.image.DetectEdges(EdgeDetection.Robinson);
            this.image.DetectEdges(EdgeDetection.Scharr);
            this.image.DetectEdges(EdgeDetection.Sobel);
        }
    }
}
