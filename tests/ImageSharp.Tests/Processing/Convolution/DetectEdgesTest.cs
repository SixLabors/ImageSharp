// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Convolution;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Convolution
{
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "OK. Used for TheoryData compatibility.")]
    public class DetectEdgesTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void DetectEdges_EdgeDetector2DProcessorDefaultsSet()
        {
            this.operations.DetectEdges();
            EdgeDetector2DProcessor processor = this.Verify<EdgeDetector2DProcessor>();

            Assert.True(processor.Grayscale);
            Assert.Equal(KnownEdgeDetectorKernels.Sobel, processor.Kernel);
        }

        [Fact]
        public void DetectEdges_Rect_EdgeDetector2DProcessorDefaultsSet()
        {
            this.operations.DetectEdges(this.rect);
            EdgeDetector2DProcessor processor = this.Verify<EdgeDetector2DProcessor>(this.rect);

            Assert.True(processor.Grayscale);
            Assert.Equal(KnownEdgeDetectorKernels.Sobel, processor.Kernel);
        }

        public static TheoryData<EdgeDetector2DKernel, bool> EdgeDetector2DKernelData =
            new TheoryData<EdgeDetector2DKernel, bool>
            {
                { KnownEdgeDetectorKernels.Kayyali, true },
                { KnownEdgeDetectorKernels.Kayyali, false },
                { KnownEdgeDetectorKernels.Prewitt, true },
                { KnownEdgeDetectorKernels.Prewitt, false },
                { KnownEdgeDetectorKernels.RobertsCross, true },
                { KnownEdgeDetectorKernels.RobertsCross, false },
                { KnownEdgeDetectorKernels.Scharr, true },
                { KnownEdgeDetectorKernels.Scharr, false },
                { KnownEdgeDetectorKernels.Sobel, true },
                { KnownEdgeDetectorKernels.Sobel, false },
            };

        [Theory]
        [MemberData(nameof(EdgeDetector2DKernelData))]
        public void DetectEdges_EdgeDetector2DProcessor_DefaultGrayScale_Set(EdgeDetector2DKernel kernel, bool _)
        {
            this.operations.DetectEdges(kernel);
            EdgeDetector2DProcessor processor = this.Verify<EdgeDetector2DProcessor>();

            Assert.True(processor.Grayscale);
            Assert.Equal(kernel, processor.Kernel);
        }

        [Theory]
        [MemberData(nameof(EdgeDetector2DKernelData))]
        public void DetectEdges_Rect_EdgeDetector2DProcessor_DefaultGrayScale_Set(EdgeDetector2DKernel kernel, bool _)
        {
            this.operations.DetectEdges(kernel, this.rect);
            EdgeDetector2DProcessor processor = this.Verify<EdgeDetector2DProcessor>(this.rect);

            Assert.True(processor.Grayscale);
            Assert.Equal(kernel, processor.Kernel);
        }

        [Theory]
        [MemberData(nameof(EdgeDetector2DKernelData))]
        public void DetectEdges_EdgeDetector2DProcessorSet(EdgeDetector2DKernel kernel, bool grayscale)
        {
            this.operations.DetectEdges(kernel, grayscale);
            EdgeDetector2DProcessor processor = this.Verify<EdgeDetector2DProcessor>();

            Assert.Equal(grayscale, processor.Grayscale);
            Assert.Equal(kernel, processor.Kernel);
        }

        [Theory]
        [MemberData(nameof(EdgeDetector2DKernelData))]
        public void DetectEdges_Rect_EdgeDetector2DProcessorSet(EdgeDetector2DKernel kernel, bool grayscale)
        {
            this.operations.DetectEdges(kernel, grayscale, this.rect);
            EdgeDetector2DProcessor processor = this.Verify<EdgeDetector2DProcessor>(this.rect);

            Assert.Equal(grayscale, processor.Grayscale);
            Assert.Equal(kernel, processor.Kernel);
        }

        public static TheoryData<EdgeDetectorKernel, bool> EdgeDetectorKernelData =
            new TheoryData<EdgeDetectorKernel, bool>
            {
                { KnownEdgeDetectorKernels.Laplacian3x3, true },
                { KnownEdgeDetectorKernels.Laplacian3x3, false },
                { KnownEdgeDetectorKernels.Laplacian5x5, true },
                { KnownEdgeDetectorKernels.Laplacian5x5, false },
                { KnownEdgeDetectorKernels.LaplacianOfGaussian, true },
                { KnownEdgeDetectorKernels.LaplacianOfGaussian, false },
            };

        [Theory]
        [MemberData(nameof(EdgeDetectorKernelData))]
        public void DetectEdges_EdgeDetectorProcessor_DefaultGrayScale_Set(EdgeDetectorKernel kernel, bool _)
        {
            this.operations.DetectEdges(kernel);
            EdgeDetectorProcessor processor = this.Verify<EdgeDetectorProcessor>();

            Assert.True(processor.Grayscale);
            Assert.Equal(kernel, processor.Kernel);
        }

        [Theory]
        [MemberData(nameof(EdgeDetectorKernelData))]
        public void DetectEdges_Rect_EdgeDetectorProcessor_DefaultGrayScale_Set(EdgeDetectorKernel kernel, bool _)
        {
            this.operations.DetectEdges(kernel, this.rect);
            EdgeDetectorProcessor processor = this.Verify<EdgeDetectorProcessor>(this.rect);

            Assert.True(processor.Grayscale);
            Assert.Equal(kernel, processor.Kernel);
        }

        [Theory]
        [MemberData(nameof(EdgeDetectorKernelData))]
        public void DetectEdges_EdgeDetectorProcessorSet(EdgeDetectorKernel kernel, bool grayscale)
        {
            this.operations.DetectEdges(kernel, grayscale);
            EdgeDetectorProcessor processor = this.Verify<EdgeDetectorProcessor>();

            Assert.Equal(grayscale, processor.Grayscale);
            Assert.Equal(kernel, processor.Kernel);
        }

        [Theory]
        [MemberData(nameof(EdgeDetectorKernelData))]
        public void DetectEdges_Rect_EdgeDetectorProcessorSet(EdgeDetectorKernel kernel, bool grayscale)
        {
            this.operations.DetectEdges(kernel, grayscale, this.rect);
            EdgeDetectorProcessor processor = this.Verify<EdgeDetectorProcessor>(this.rect);

            Assert.Equal(grayscale, processor.Grayscale);
            Assert.Equal(kernel, processor.Kernel);
        }

        public static TheoryData<EdgeDetectorCompassKernel, bool> EdgeDetectorCompassKernelData =
            new TheoryData<EdgeDetectorCompassKernel, bool>
            {
                { KnownEdgeDetectorKernels.Kirsch, true },
                { KnownEdgeDetectorKernels.Kirsch, false },
                { KnownEdgeDetectorKernels.Robinson, true },
                { KnownEdgeDetectorKernels.Robinson, false },
            };

        [Theory]
        [MemberData(nameof(EdgeDetectorCompassKernelData))]
        public void DetectEdges_EdgeDetectorCompassProcessor_DefaultGrayScale_Set(EdgeDetectorCompassKernel kernel, bool _)
        {
            this.operations.DetectEdges(kernel);
            EdgeDetectorCompassProcessor processor = this.Verify<EdgeDetectorCompassProcessor>();

            Assert.True(processor.Grayscale);
            Assert.Equal(kernel, processor.Kernel);
        }

        [Theory]
        [MemberData(nameof(EdgeDetectorCompassKernelData))]
        public void DetectEdges_Rect_EdgeDetectorCompassProcessor_DefaultGrayScale_Set(EdgeDetectorCompassKernel kernel, bool _)
        {
            this.operations.DetectEdges(kernel, this.rect);
            EdgeDetectorCompassProcessor processor = this.Verify<EdgeDetectorCompassProcessor>(this.rect);

            Assert.True(processor.Grayscale);
            Assert.Equal(kernel, processor.Kernel);
        }

        [Theory]
        [MemberData(nameof(EdgeDetectorCompassKernelData))]
        public void DetectEdges_EdgeDetectorCompassProcessorSet(EdgeDetectorCompassKernel kernel, bool grayscale)
        {
            this.operations.DetectEdges(kernel, grayscale);
            EdgeDetectorCompassProcessor processor = this.Verify<EdgeDetectorCompassProcessor>();

            Assert.Equal(grayscale, processor.Grayscale);
            Assert.Equal(kernel, processor.Kernel);
        }

        [Theory]
        [MemberData(nameof(EdgeDetectorCompassKernelData))]
        public void DetectEdges_Rect_EdgeDetectorCompassProcessorSet(EdgeDetectorCompassKernel kernel, bool grayscale)
        {
            this.operations.DetectEdges(kernel, grayscale, this.rect);
            EdgeDetectorCompassProcessor processor = this.Verify<EdgeDetectorCompassProcessor>(this.rect);

            Assert.Equal(grayscale, processor.Grayscale);
            Assert.Equal(kernel, processor.Kernel);
        }
    }
}
