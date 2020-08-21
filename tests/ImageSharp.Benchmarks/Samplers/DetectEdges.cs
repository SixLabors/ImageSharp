// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks
{
    using System.IO;

    using BenchmarkDotNet.Attributes;

    using SixLabors.ImageSharp.Processing;
    using CoreImage = SixLabors.ImageSharp.Image;

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
            this.image.Mutate(x => x.DetectEdges(KnownEdgeDetectorKernels.Kayyali));
            this.image.Mutate(x => x.DetectEdges(KnownEdgeDetectorKernels.Kayyali));
            this.image.Mutate(x => x.DetectEdges(KnownEdgeDetectorKernels.Kirsch));
            this.image.Mutate(x => x.DetectEdges(KnownEdgeDetectorKernels.Laplacian3x3));
            this.image.Mutate(x => x.DetectEdges(KnownEdgeDetectorKernels.Laplacian5x5));
            this.image.Mutate(x => x.DetectEdges(KnownEdgeDetectorKernels.LaplacianOfGaussian));
            this.image.Mutate(x => x.DetectEdges(KnownEdgeDetectorKernels.Prewitt));
            this.image.Mutate(x => x.DetectEdges(KnownEdgeDetectorKernels.RobertsCross));
            this.image.Mutate(x => x.DetectEdges(KnownEdgeDetectorKernels.Robinson));
            this.image.Mutate(x => x.DetectEdges(KnownEdgeDetectorKernels.Scharr));
            this.image.Mutate(x => x.DetectEdges(KnownEdgeDetectorKernels.Sobel));
        }
    }
}
