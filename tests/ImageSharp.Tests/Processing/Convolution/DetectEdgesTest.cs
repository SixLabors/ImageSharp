// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.ImageSharp.Processing.Processors.Convolution;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Convolution
{
    public class DetectEdgesTest : BaseImageOperationsExtensionTest
    {

        [Fact]
        public void DetectEdges_SobelProcessorDefaultsSet()
        {
            this.operations.DetectEdges();

            // TODO: Enable once we have updated the images
            // SobelProcessor<Rgba32> processor = this.Verify<SobelProcessor<Rgba32>>();
            // Assert.True(processor.Grayscale);
        }

        [Fact]
        public void DetectEdges_Rect_SobelProcessorDefaultsSet()
        {
            this.operations.DetectEdges(this.rect);

            // TODO: Enable once we have updated the images
            // SobelProcessor<Rgba32> processor = this.Verify<SobelProcessor<Rgba32>>(this.rect);
            // Assert.True(processor.Grayscale);
        }
        public static IEnumerable<object[]> EdgeDetectionTheoryData => new[] {
            new object[]{ new TestType<KayyaliProcessor<Rgba32>>(), EdgeDetectionOperators.Kayyali },
            new object[]{ new TestType<KirschProcessor<Rgba32>>(), EdgeDetectionOperators.Kirsch },
            new object[]{ new TestType<Laplacian3x3Processor<Rgba32>>(), EdgeDetectionOperators.Laplacian3x3 },
            new object[]{ new TestType<Laplacian5x5Processor<Rgba32>>(), EdgeDetectionOperators.Laplacian5x5 },
            new object[]{ new TestType<LaplacianOfGaussianProcessor<Rgba32>>(), EdgeDetectionOperators.LaplacianOfGaussian },
            new object[]{ new TestType<PrewittProcessor<Rgba32>>(), EdgeDetectionOperators.Prewitt },
            new object[]{ new TestType<RobertsCrossProcessor<Rgba32>>(), EdgeDetectionOperators.RobertsCross },
            new object[]{ new TestType<RobinsonProcessor<Rgba32>>(), EdgeDetectionOperators.Robinson },
            new object[]{ new TestType<ScharrProcessor<Rgba32>>(), EdgeDetectionOperators.Scharr },
            new object[]{ new TestType<SobelProcessor<Rgba32>>(), EdgeDetectionOperators.Sobel },
        };

        [Theory]
        [MemberData(nameof(EdgeDetectionTheoryData))]
        public void DetectEdges_filter_SobelProcessorDefaultsSet<TProcessor>(TestType<TProcessor> type, EdgeDetectionOperators filter)
            where TProcessor : IEdgeDetectorProcessor<Rgba32>
        {
            this.operations.DetectEdges(filter);

            // TODO: Enable once we have updated the images
            // var processor = this.Verify<TProcessor>();
            // Assert.True(processor.Grayscale);
        }

        [Theory]
        [MemberData(nameof(EdgeDetectionTheoryData))]
        public void DetectEdges_filter_grayscale_SobelProcessorDefaultsSet<TProcessor>(TestType<TProcessor> type, EdgeDetectionOperators filter)
            where TProcessor : IEdgeDetectorProcessor<Rgba32>
        {
            bool grey = (int)filter % 2 == 0;
            this.operations.DetectEdges(filter, grey);

            // TODO: Enable once we have updated the images
            // var processor = this.Verify<TProcessor>()
            // Assert.Equal(grey, processor.Grayscale);
        }
    }
}