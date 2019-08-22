// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Convolution;
using SixLabors.ImageSharp.Tests.TestUtilities;

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
            SobelProcessor processor = this.Verify<SobelProcessor>();
            Assert.True(processor.Grayscale);
        }

        [Fact]
        public void DetectEdges_Rect_SobelProcessorDefaultsSet()
        {
            this.operations.DetectEdges(this.rect);

            // TODO: Enable once we have updated the images
            SobelProcessor processor = this.Verify<SobelProcessor>(this.rect);
            Assert.True(processor.Grayscale);
        }
        public static IEnumerable<object[]> EdgeDetectionTheoryData => new[] {
            new object[]{ new TestType<KayyaliProcessor>(), EdgeDetectionOperators.Kayyali },
            new object[]{ new TestType<KirschProcessor>(), EdgeDetectionOperators.Kirsch },
            new object[]{ new TestType<Laplacian3x3Processor>(), EdgeDetectionOperators.Laplacian3x3 },
            new object[]{ new TestType<Laplacian5x5Processor>(), EdgeDetectionOperators.Laplacian5x5 },
            new object[]{ new TestType<LaplacianOfGaussianProcessor>(), EdgeDetectionOperators.LaplacianOfGaussian },
            new object[]{ new TestType<PrewittProcessor>(), EdgeDetectionOperators.Prewitt },
            new object[]{ new TestType<RobertsCrossProcessor>(), EdgeDetectionOperators.RobertsCross },
            new object[]{ new TestType<RobinsonProcessor>(), EdgeDetectionOperators.Robinson },
            new object[]{ new TestType<ScharrProcessor>(), EdgeDetectionOperators.Scharr },
            new object[]{ new TestType<SobelProcessor>(), EdgeDetectionOperators.Sobel },
        };

        [Theory]
        [MemberData(nameof(EdgeDetectionTheoryData))]
        public void DetectEdges_filter_SobelProcessorDefaultsSet<TProcessor>(TestType<TProcessor> type, EdgeDetectionOperators filter)
            where TProcessor : EdgeDetectorProcessor
        {
            this.operations.DetectEdges(filter);

            // TODO: Enable once we have updated the images
            var processor = this.Verify<TProcessor>();
            Assert.True(processor.Grayscale);
        }

        [Theory]
        [MemberData(nameof(EdgeDetectionTheoryData))]
        public void DetectEdges_filter_grayscale_SobelProcessorDefaultsSet<TProcessor>(TestType<TProcessor> type, EdgeDetectionOperators filter)
            where TProcessor : EdgeDetectorProcessor
        {
            bool grey = (int)filter % 2 == 0;
            this.operations.DetectEdges(filter, grey);

            // TODO: Enable once we have updated the images
            var processor = this.Verify<TProcessor>();
            Assert.Equal(grey, processor.Grayscale);
        }
    }
}