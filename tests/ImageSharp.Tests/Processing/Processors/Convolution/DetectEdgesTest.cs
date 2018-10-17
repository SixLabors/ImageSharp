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
        private static readonly ImageComparer ValidatorComparer = ImageComparer.TolerantPercentage(0.0456F);

        public static readonly string[] CommonTestImages = { TestImages.Png.Bike };

        public static readonly TheoryData<EdgeDetectionOperators> DetectEdgesFilters = new TheoryData<EdgeDetectionOperators>
        {
            EdgeDetectionOperators.Kayyali,
            EdgeDetectionOperators.Kirsch,
            EdgeDetectionOperators.Laplacian3x3,
            EdgeDetectionOperators.Laplacian5x5,
            EdgeDetectionOperators.LaplacianOfGaussian,
            EdgeDetectionOperators.Prewitt,
            EdgeDetectionOperators.RobertsCross,
            EdgeDetectionOperators.Robinson,
            EdgeDetectionOperators.Scharr,
            EdgeDetectionOperators.Sobel
        };

        [Theory]
        [WithFileCollection(nameof(CommonTestImages), DefaultPixelType)]
        public void DetectEdges_WorksOnWrappedMemoryImage<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            provider.RunValidatingProcessorTestOnWrappedMemoryImage(
                ctx =>
                    {
                        Size size = ctx.GetCurrentSize();
                        var bounds = new Rectangle(10, 10, size.Width / 2, size.Height / 2);
                        ctx.DetectEdges(bounds);
                    },
                useReferenceOutputFrom: nameof(this.DetectEdges_InBox));
        }

        [Theory]
        [WithTestPatternImages(nameof(DetectEdgesFilters), 100, 100, DefaultPixelType)]
        [WithFileCollection(nameof(CommonTestImages), nameof(DetectEdgesFilters), DefaultPixelType)]
        public void DetectEdges_WorksWithAllFilters<TPixel>(TestImageProvider<TPixel> provider, EdgeDetectionOperators detector)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.DetectEdges(detector));
                image.DebugSave(provider, detector.ToString());
                image.CompareToReferenceOutput(ValidatorComparer, provider, detector.ToString());
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
                image.CompareToReferenceOutput(ValidatorComparer, provider);
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
                image.CompareToReferenceOutput(ValidatorComparer, provider);
            }
        }
    }
}