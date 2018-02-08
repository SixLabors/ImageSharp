// <copyright file="DetectEdges.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace SixLabors.ImageSharp.Benchmarks
{
    using System.IO;

    using BenchmarkDotNet.Attributes;

    using SixLabors.ImageSharp.Processing;

    using CoreImage = ImageSharp.Image;

    public class DetectEdges : BenchmarkBase
    {
        private Image<Rgba32> image;

        [GlobalSetup]
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

        [GlobalCleanup]
        public void Cleanup()
        {
            this.image.Dispose();
        }

        [Benchmark(Description = "ImageSharp DetectEdges")]
        public void ImageProcessorCoreDetectEdges()
        {
            this.image.Mutate(x => x.DetectEdges(EdgeDetection.Kayyali));
            this.image.Mutate(x => x.DetectEdges(EdgeDetection.Kayyali));
            this.image.Mutate(x => x.DetectEdges(EdgeDetection.Kirsch));
            this.image.Mutate(x => x.DetectEdges(EdgeDetection.Lapacian3X3));
            this.image.Mutate(x => x.DetectEdges(EdgeDetection.Lapacian5X5));
            this.image.Mutate(x => x.DetectEdges(EdgeDetection.LaplacianOfGaussian));
            this.image.Mutate(x => x.DetectEdges(EdgeDetection.Prewitt));
            this.image.Mutate(x => x.DetectEdges(EdgeDetection.RobertsCross));
            this.image.Mutate(x => x.DetectEdges(EdgeDetection.Robinson));
            this.image.Mutate(x => x.DetectEdges(EdgeDetection.Scharr));
            this.image.Mutate(x => x.DetectEdges(EdgeDetection.Sobel));
        }
    }
}
