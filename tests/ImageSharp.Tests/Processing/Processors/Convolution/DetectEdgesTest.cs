// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Convolution;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using Xunit;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Processing.Processors.Convolution
{
    [GroupOutput("Convolution")]
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "OK. Used for TheoryData compatibility.")]
    public class DetectEdgesTest
    {
        private static readonly ImageComparer OpaqueComparer = ImageComparer.TolerantPercentage(0.01F);

        private static readonly ImageComparer TransparentComparer = ImageComparer.TolerantPercentage(0.5F);

        public static readonly string[] TestImages = { Tests.TestImages.Png.Bike };

        public const PixelTypes CommonNonDefaultPixelTypes = PixelTypes.Rgba32 | PixelTypes.Bgra32 | PixelTypes.RgbaVector;

        public static readonly TheoryData<EdgeDetectorKernel, string> DetectEdgesFilters
            = new TheoryData<EdgeDetectorKernel, string>
        {
            { KnownEdgeDetectorKernels.Laplacian3x3, nameof(KnownEdgeDetectorKernels.Laplacian3x3) },
            { KnownEdgeDetectorKernels.Laplacian5x5, nameof(KnownEdgeDetectorKernels.Laplacian5x5) },
            { KnownEdgeDetectorKernels.LaplacianOfGaussian, nameof(KnownEdgeDetectorKernels.LaplacianOfGaussian) },
        };

        public static readonly TheoryData<EdgeDetector2DKernel, string> DetectEdges2DFilters
            = new TheoryData<EdgeDetector2DKernel, string>
        {
            { KnownEdgeDetectorKernels.Kayyali, nameof(KnownEdgeDetectorKernels.Kayyali) },
            { KnownEdgeDetectorKernels.Prewitt, nameof(KnownEdgeDetectorKernels.Prewitt) },
            { KnownEdgeDetectorKernels.RobertsCross, nameof(KnownEdgeDetectorKernels.RobertsCross) },
            { KnownEdgeDetectorKernels.Scharr, nameof(KnownEdgeDetectorKernels.Scharr) },
            { KnownEdgeDetectorKernels.Sobel, nameof(KnownEdgeDetectorKernels.Sobel) },
        };

        public static readonly TheoryData<EdgeDetectorCompassKernel, string> DetectEdgesCompassFilters
            = new TheoryData<EdgeDetectorCompassKernel, string>
        {
            { KnownEdgeDetectorKernels.Kirsch, nameof(KnownEdgeDetectorKernels.Kirsch) },
            { KnownEdgeDetectorKernels.Robinson, nameof(KnownEdgeDetectorKernels.Robinson) },
        };

        [Theory]
        [WithFileCollection(nameof(TestImages), PixelTypes.Rgba32)]
        public void DetectEdges_WorksOnWrappedMemoryImage<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            provider.RunValidatingProcessorTestOnWrappedMemoryImage(
                ctx =>
                    {
                        Size size = ctx.GetCurrentSize();
                        var bounds = new Rectangle(10, 10, size.Width / 2, size.Height / 2);
                        ctx.DetectEdges(bounds);
                    },
                comparer: OpaqueComparer,
                useReferenceOutputFrom: nameof(this.DetectEdges_InBox));
        }

        [Theory]
        [WithTestPatternImages(nameof(DetectEdgesFilters), 100, 100, PixelTypes.Rgba32)]
        [WithFileCollection(nameof(TestImages), nameof(DetectEdgesFilters), PixelTypes.Rgba32)]
        public void DetectEdges_WorksWithAllFilters<TPixel>(
            TestImageProvider<TPixel> provider,
            EdgeDetectorKernel detector,
            string name)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            bool hasAlpha = provider.SourceFileOrDescription.Contains("TestPattern");
            ImageComparer comparer = hasAlpha ? TransparentComparer : OpaqueComparer;
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.DetectEdges(detector));
                image.DebugSave(provider, name);
                image.CompareToReferenceOutput(comparer, provider, name);
            }
        }

        [Theory]
        [WithTestPatternImages(nameof(DetectEdges2DFilters), 100, 100, PixelTypes.Rgba32)]
        [WithFileCollection(nameof(TestImages), nameof(DetectEdges2DFilters), PixelTypes.Rgba32)]
        public void DetectEdges2D_WorksWithAllFilters<TPixel>(
            TestImageProvider<TPixel> provider,
            EdgeDetector2DKernel detector,
            string name)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            bool hasAlpha = provider.SourceFileOrDescription.Contains("TestPattern");
            ImageComparer comparer = hasAlpha ? TransparentComparer : OpaqueComparer;
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.DetectEdges(detector));
                image.DebugSave(provider, name);
                image.CompareToReferenceOutput(comparer, provider, name);
            }
        }

        [Theory]
        [WithTestPatternImages(nameof(DetectEdgesCompassFilters), 100, 100, PixelTypes.Rgba32)]
        [WithFileCollection(nameof(TestImages), nameof(DetectEdgesCompassFilters), PixelTypes.Rgba32)]
        public void DetectEdgesCompass_WorksWithAllFilters<TPixel>(
            TestImageProvider<TPixel> provider,
            EdgeDetectorCompassKernel detector,
            string name)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            bool hasAlpha = provider.SourceFileOrDescription.Contains("TestPattern");
            ImageComparer comparer = hasAlpha ? TransparentComparer : OpaqueComparer;
            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.DetectEdges(detector));
                image.DebugSave(provider, name);
                image.CompareToReferenceOutput(comparer, provider, name);
            }
        }

        [Theory]
        [WithFileCollection(nameof(TestImages), CommonNonDefaultPixelTypes)]
        public void DetectEdges_IsNotBoundToSinglePixelType<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            // James:
            // I think our comparison is not accurate enough (nor can be) for RgbaVector.
            // The image pixels are identical according to BeyondCompare.
            ImageComparer comparer = typeof(TPixel) == typeof(RgbaVector) ?
                ImageComparer.TolerantPercentage(1f) :
                OpaqueComparer;

            using (Image<TPixel> image = provider.GetImage())
            {
                image.Mutate(x => x.DetectEdges());
                image.DebugSave(provider);
                image.CompareToReferenceOutput(comparer, provider);
            }
        }

        [Theory]
        [WithFile(Tests.TestImages.Gif.Giphy, PixelTypes.Rgba32)]
        public void DetectEdges_IsAppliedToAllFrames<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : unmanaged, IPixel<TPixel>
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
            where TPixel : unmanaged, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                var bounds = new Rectangle(10, 10, image.Width / 2, image.Height / 2);

                image.Mutate(x => x.DetectEdges(bounds));
                image.DebugSave(provider);
                image.CompareToReferenceOutput(OpaqueComparer, provider);
            }
        }

        [Theory]
        [WithFile(Tests.TestImages.Png.Bike, nameof(DetectEdgesFilters), PixelTypes.Rgba32)]
        public void WorksWithDiscoBuffers<TPixel>(
            TestImageProvider<TPixel> provider,
            EdgeDetectorKernel detector,
            string _)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            provider.RunBufferCapacityLimitProcessorTest(
                41,
                c => c.DetectEdges(detector),
                detector);
        }

        [Theory]
        [WithFile(Tests.TestImages.Png.Bike, nameof(DetectEdges2DFilters), PixelTypes.Rgba32)]
        public void WorksWithDiscoBuffers2D<TPixel>(
            TestImageProvider<TPixel> provider,
            EdgeDetector2DKernel detector,
            string _)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            provider.RunBufferCapacityLimitProcessorTest(
                41,
                c => c.DetectEdges(detector),
                detector);
        }

        [Theory]
        [WithFile(Tests.TestImages.Png.Bike, nameof(DetectEdgesCompassFilters), PixelTypes.Rgba32)]
        public void WorksWithDiscoBuffersCompass<TPixel>(
            TestImageProvider<TPixel> provider,
            EdgeDetectorCompassKernel detector,
            string _)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            provider.RunBufferCapacityLimitProcessorTest(
                41,
                c => c.DetectEdges(detector),
                detector);
        }
    }
}
