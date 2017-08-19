// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Convolution
{
    public class DetectEdgesTest : FileTestBase
    {
        public static readonly TheoryData<EdgeDetection> DetectEdgesFilters
        = new TheoryData<EdgeDetection>
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
        [WithFileCollection(nameof(DefaultFiles), nameof(DetectEdgesFilters), DefaultPixelType)]
        public void ImageShouldApplyDetectEdgesFilter<TPixel>(TestImageProvider<TPixel> provider, EdgeDetection detector)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.DetectEdges(detector));
                image.DebugSave(provider, detector.ToString(), Extensions.Bmp);
            }
        }

        [Theory]
        [WithFileCollection(nameof(DefaultFiles), nameof(DetectEdgesFilters), DefaultPixelType)]
        public void ImageShouldApplyDetectEdgesFilterInBox<TPixel>(TestImageProvider<TPixel> provider, EdgeDetection detector)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> source = provider.GetImage())
            using (var image = source.Clone())
            {
                var bounds = new Rectangle(10, 10, image.Width / 2, image.Height / 2);

                image.Mutate(x => x.DetectEdges(detector, bounds));
                image.DebugSave(provider, detector.ToString(), Extensions.Bmp);

                ImageComparer.EnsureProcessorChangesAreConstrained(source, image, bounds);
            }
        }
    }
}