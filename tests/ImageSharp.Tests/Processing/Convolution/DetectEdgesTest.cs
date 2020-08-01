// Copyright (c) Six Labors.
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

        public static IEnumerable<object[]> EdgeDetectionTheoryData => new[]
        {
            new object[] { new TestType<KayyaliProcessor>(), KnownEdgeDetectionOperators.Kayyali },
            new object[] { new TestType<KirschProcessor>(), KnownEdgeDetectionOperators.Kirsch },
            new object[] { new TestType<Laplacian3x3Processor>(), KnownEdgeDetectionOperators.Laplacian3x3 },
            new object[] { new TestType<Laplacian5x5Processor>(), KnownEdgeDetectionOperators.Laplacian5x5 },
            new object[] { new TestType<LaplacianOfGaussianProcessor>(), KnownEdgeDetectionOperators.LaplacianOfGaussian },
            new object[] { new TestType<PrewittProcessor>(), KnownEdgeDetectionOperators.Prewitt },
            new object[] { new TestType<RobertsCrossProcessor>(), KnownEdgeDetectionOperators.RobertsCross },
            new object[] { new TestType<RobinsonProcessor>(), KnownEdgeDetectionOperators.Robinson },
            new object[] { new TestType<ScharrProcessor>(), KnownEdgeDetectionOperators.Scharr },
            new object[] { new TestType<SobelProcessor>(), KnownEdgeDetectionOperators.Sobel },
        };

        [Theory]
        [MemberData(nameof(EdgeDetectionTheoryData))]
        public void DetectEdges_filter_SobelProcessorDefaultsSet<TProcessor>(TestType<TProcessor> type, KnownEdgeDetectionOperators filter)
            where TProcessor : EdgeDetectorProcessor
        {
            this.operations.DetectEdges(filter);

            // TODO: Enable once we have updated the images
            var processor = this.Verify<TProcessor>();
            Assert.True(processor.Grayscale);
        }

        [Theory]
        [MemberData(nameof(EdgeDetectionTheoryData))]
        public void DetectEdges_filter_grayscale_SobelProcessorDefaultsSet<TProcessor>(TestType<TProcessor> type, KnownEdgeDetectionOperators filter)
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
