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
            if (image == null)
            {
                using(FileStream stream = File.OpenRead("../ImageProcessorCore.Tests/TestImages/Formats/Bmp/Car.bmp"))
                {
                    image = new CoreImage(stream);
                }
            }
        }

        [Benchmark(Description = "ImageProcessorCore DetectEdges")]
        public void ImageProcessorCoreDetectEdges()
        {
            image.DetectEdges(EdgeDetection.Kayyali);
            image.DetectEdges(EdgeDetection.Kayyali);
            image.DetectEdges(EdgeDetection.Kirsch);
            image.DetectEdges(EdgeDetection.Lapacian3X3);
            image.DetectEdges(EdgeDetection.Lapacian5X5);
            image.DetectEdges(EdgeDetection.LaplacianOfGaussian);
            image.DetectEdges(EdgeDetection.Prewitt);
            image.DetectEdges(EdgeDetection.RobertsCross);
            image.DetectEdges(EdgeDetection.Robinson);
            image.DetectEdges(EdgeDetection.Scharr);
            image.DetectEdges(EdgeDetection.Sobel);
        }
    }
}
