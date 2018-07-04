// <copyright file="DetectEdges.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using SixLabors.ImageSharp.PixelFormats;

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
            this.image.Mutate(x => x.DetectEdges(EdgeDetectionOperators.Kayyali));
            this.image.Mutate(x => x.DetectEdges(EdgeDetectionOperators.Kayyali));
            this.image.Mutate(x => x.DetectEdges(EdgeDetectionOperators.Kirsch));
            this.image.Mutate(x => x.DetectEdges(EdgeDetectionOperators.Laplacian3x3));
            this.image.Mutate(x => x.DetectEdges(EdgeDetectionOperators.Laplacian5x5));
            this.image.Mutate(x => x.DetectEdges(EdgeDetectionOperators.LaplacianOfGaussian));
            this.image.Mutate(x => x.DetectEdges(EdgeDetectionOperators.Prewitt));
            this.image.Mutate(x => x.DetectEdges(EdgeDetectionOperators.RobertsCross));
            this.image.Mutate(x => x.DetectEdges(EdgeDetectionOperators.Robinson));
            this.image.Mutate(x => x.DetectEdges(EdgeDetectionOperators.Scharr));
            this.image.Mutate(x => x.DetectEdges(EdgeDetectionOperators.Sobel));
        }
    }
}
