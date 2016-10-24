// <copyright file="DetectEdges.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Benchmarks
{
    using System.IO;

    using BenchmarkDotNet.Attributes;

    using CoreImage = ImageProcessorCore.Image;

    public class DetectEdges
    {
        private CoreImage image;

        [Setup]
        public void ReadImage()
        {
            if (this.image == null)
            {
                using (FileStream stream = File.OpenRead("../ImageProcessorCore.Tests/TestImages/Formats/Bmp/Car.bmp"))
                {
                    this.image = new CoreImage(stream);
                }
            }
        }

        [Benchmark(Description = "ImageProcessorCore DetectEdges")]
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
