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
    [GroupOutput("Convolution")]
    public class DetectEdgesTest
    {
        // I think our comparison is not accurate enough (nor can be) for RgbaVector.
        // The image pixels are identical according to BeyondCompare.
        private static readonly ImageComparer ValidatorComparer = ImageComparer.TolerantPercentage(0.0456F);

        public static readonly string[] TestImages = { Tests.TestImages.Png.Bike };
        
        public const PixelTypes CommonNonDefaultPixelTypes = PixelTypes.Rgba32 | PixelTypes.Bgra32 | PixelTypes.RgbaVector;

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
        [WithFileCollection(nameof(TestImages), PixelTypes.Rgba32)]
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
                comparer: ValidatorComparer,
                useReferenceOutputFrom: nameof(this.DetectEdges_InBox));
        }

        [Theory]
        [WithTestPatternImages(nameof(DetectEdgesFilters), 100, 100, PixelTypes.Rgba32)]
        [WithFileCollection(nameof(TestImages), nameof(DetectEdgesFilters), PixelTypes.Rgba32)]
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
        [WithFileCollection(nameof(TestImages), CommonNonDefaultPixelTypes)]
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
        [WithFile(Tests.TestImages.Gif.Giphy, PixelTypes.Rgba32)]
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
        [WithFileCollection(nameof(TestImages), PixelTypes.Rgba32)]
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