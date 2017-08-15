﻿// <copyright file="DetectEdgesTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Processors.Convolution
{
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing;
    using SixLabors.Primitives;
    using Xunit;

    public class DetectEdgesTest : FileTestBase
    {
        public static readonly string[] CommonTestImages = { TestImages.Png.Bike };
        public static readonly string[] GrayscaleTestImages = { TestImages.Png.BikeGrayscale };

        public static readonly TheoryData<EdgeDetection> DetectEdgesFilters = new TheoryData<EdgeDetection>
        {
            EdgeDetection.Kayyali,
            EdgeDetection.Kirsch,
            EdgeDetection.Lapacian3X3,
            EdgeDetection.Lapacian5X5,
            EdgeDetection.LaplacianOfGaussian,
            EdgeDetection.Prewitt,
            EdgeDetection.RobertsCross,
            EdgeDetection.Robinson,
            EdgeDetection.Scharr,
            EdgeDetection.Sobel
        };

        [Theory]
        [WithTestPatternImages(nameof(DetectEdgesFilters), 100, 100, DefaultPixelType)]
        [WithFileCollection(nameof(CommonTestImages), nameof(DetectEdgesFilters), DefaultPixelType)]
        public void DetectEdges<TPixel>(TestImageProvider<TPixel> provider, EdgeDetection detector)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.DetectEdges(detector));
                image.DebugSave(provider, detector.ToString(), grayscale: true);
            }
        }

        [Theory]
        [WithFileCollection(nameof(GrayscaleTestImages), DefaultPixelType)]
        public void DetectEdgesInBox<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (Image<TPixel> image = source.Clone())
            {
                var bounds = new Rectangle(10, 10, image.Width / 2, image.Height / 2);

                image.Mutate(x => x.DetectEdges(bounds));
                image.DebugSave(provider, grayscale: true);

                // TODO: We don't need this any longer after switching to ReferenceImages
                PercentageImageComparer.EnsureProcessorChangesAreConstrained(source, image, bounds);
            }
        }
    }
}