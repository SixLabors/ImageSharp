// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

using SixLabors.Primitives;
using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Processing.Processors.Convolution
{
    public class DetectEdgesTest : FileTestBase
    {
        public static readonly string[] CommonTestImages = { TestImages.Png.Bike };
        
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
        public void DetectEdges_WorksWithAllFilters<TPixel>(TestImageProvider<TPixel> provider, EdgeDetection detector)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.DetectEdges(detector));
                image.DebugSave(provider, detector.ToString());
                image.CompareToReferenceOutput(provider, detector.ToString());
            }
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), CommonNonDefaultPixelTypes)]
        public void DetectEdges_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.DetectEdges());
                image.DebugSave(provider);
                image.CompareToReferenceOutput(provider);
            }
        }

        [Theory]
        [WithFile(TestImages.Gif.Giphy, DefaultPixelType)]
        public void DetectEdges_IsAppliedToAllFrames<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.DetectEdges());
                image.DebugSave(provider, extension: "gif");
            }
        }

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), DefaultPixelType)]
        public void DetectEdges_InBox<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                var bounds = new Rectangle(10, 10, image.Width / 2, image.Height / 2);

                image.Mutate(x => x.DetectEdges(bounds));
                image.DebugSave(provider);
                image.CompareToReferenceOutput(provider);

                // TODO: We don't need this any longer after switching to ReferenceImages
                //ImageComparer.Tolerant().EnsureProcessorChangesAreConstrained(source, image, bounds);
            }
        }
    }
}