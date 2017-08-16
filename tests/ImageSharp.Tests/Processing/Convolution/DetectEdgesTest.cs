// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors;
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
            var processor = this.Verify<SobelProcessor<Rgba32>>();

            Assert.True(processor.Grayscale);
        }

        [Fact]
        public void DetectEdges_Rect_SobelProcessorDefaultsSet()
        {
            this.operations.DetectEdges(this.rect);
            var processor = this.Verify<SobelProcessor<Rgba32>>(this.rect);

            Assert.True(processor.Grayscale);
        }
        public static IEnumerable<object[]> EdgeDetectionTheoryData => new[] {
            new object[]{ new TestType<KayyaliProcessor<Rgba32>>(), EdgeDetection.Kayyali },
            new object[]{ new TestType<KirschProcessor<Rgba32>>(), EdgeDetection.Kirsch },
            new object[]{ new TestType<Laplacian3X3Processor<Rgba32>>(), EdgeDetection.Lapacian3X3 },
            new object[]{ new TestType<Laplacian5X5Processor<Rgba32>>(), EdgeDetection.Lapacian5X5 },
            new object[]{ new TestType<LaplacianOfGaussianProcessor<Rgba32>>(), EdgeDetection.LaplacianOfGaussian },
            new object[]{ new TestType<PrewittProcessor<Rgba32>>(), EdgeDetection.Prewitt },
            new object[]{ new TestType<RobertsCrossProcessor<Rgba32>>(), EdgeDetection.RobertsCross },
            new object[]{ new TestType<RobinsonProcessor<Rgba32>>(), EdgeDetection.Robinson },
            new object[]{ new TestType<ScharrProcessor<Rgba32>>(), EdgeDetection.Scharr },
            new object[]{ new TestType<SobelProcessor<Rgba32>>(), EdgeDetection.Sobel },
        };

        [Theory]
        [MemberData(nameof(EdgeDetectionTheoryData))]
        public void DetectEdges_filter_SobelProcessorDefaultsSet<TProcessor>(TestType<TProcessor> type, EdgeDetection filter)
            where TProcessor : IEdgeDetectorProcessor<Rgba32>
        {
            this.operations.DetectEdges(filter);
            var processor = this.Verify<TProcessor>();

            Assert.True(processor.Grayscale);
        }

        [Theory]
        [MemberData(nameof(EdgeDetectionTheoryData))]
        public void DetectEdges_filter_grayscale_SobelProcessorDefaultsSet<TProcessor>(TestType<TProcessor> type, EdgeDetection filter)
            where TProcessor : IEdgeDetectorProcessor<Rgba32>
        {
            var grey = (int)filter % 2 == 0;
            this.operations.DetectEdges(filter, grey);
            var processor = this.Verify<TProcessor>();

            Assert.Equal(grey, processor.Grayscale);
        }
    }
}