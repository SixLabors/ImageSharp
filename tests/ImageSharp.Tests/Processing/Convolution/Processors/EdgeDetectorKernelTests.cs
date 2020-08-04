// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Convolution;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Convolution.Processors
{
    public class EdgeDetectorKernelTests
    {
        [Fact]
        public void EdgeDetectorKernelEqualityOperatorTest()
        {
            EdgeDetectorKernel kernel0 = KnownEdgeDetectorKernels.Laplacian3x3;
            EdgeDetectorKernel kernel1 = KnownEdgeDetectorKernels.Laplacian3x3;
            EdgeDetectorKernel kernel2 = KnownEdgeDetectorKernels.Laplacian5x5;

            Assert.True(kernel0 == kernel1);
            Assert.False(kernel0 != kernel1);

            Assert.True(kernel0 != kernel2);
            Assert.False(kernel0 == kernel2);

            Assert.True(kernel0.Equals((object)kernel1));
            Assert.True(kernel0.Equals(kernel1));

            Assert.False(kernel0.Equals((object)kernel2));
            Assert.False(kernel0.Equals(kernel2));

            Assert.Equal(kernel0.GetHashCode(), kernel1.GetHashCode());
            Assert.NotEqual(kernel0.GetHashCode(), kernel2.GetHashCode());
        }

        [Fact]
        public void EdgeDetector2DKernelEqualityOperatorTest()
        {
            EdgeDetector2DKernel kernel0 = KnownEdgeDetectorKernels.Prewitt;
            EdgeDetector2DKernel kernel1 = KnownEdgeDetectorKernels.Prewitt;
            EdgeDetector2DKernel kernel2 = KnownEdgeDetectorKernels.RobertsCross;

            Assert.True(kernel0 == kernel1);
            Assert.False(kernel0 != kernel1);

            Assert.True(kernel0 != kernel2);
            Assert.False(kernel0 == kernel2);

            Assert.True(kernel0.Equals((object)kernel1));
            Assert.True(kernel0.Equals(kernel1));

            Assert.False(kernel0.Equals((object)kernel2));
            Assert.False(kernel0.Equals(kernel2));

            Assert.Equal(kernel0.GetHashCode(), kernel1.GetHashCode());
            Assert.NotEqual(kernel0.GetHashCode(), kernel2.GetHashCode());
        }

        [Fact]
        public void EdgeDetectorCompassKernelEqualityOperatorTest()
        {
            EdgeDetectorCompassKernel kernel0 = KnownEdgeDetectorKernels.Kirsch;
            EdgeDetectorCompassKernel kernel1 = KnownEdgeDetectorKernels.Kirsch;
            EdgeDetectorCompassKernel kernel2 = KnownEdgeDetectorKernels.Robinson;

            Assert.True(kernel0 == kernel1);
            Assert.False(kernel0 != kernel1);

            Assert.True(kernel0 != kernel2);
            Assert.False(kernel0 == kernel2);

            Assert.True(kernel0.Equals((object)kernel1));
            Assert.True(kernel0.Equals(kernel1));

            Assert.False(kernel0.Equals((object)kernel2));
            Assert.False(kernel0.Equals(kernel2));

            Assert.Equal(kernel0.GetHashCode(), kernel1.GetHashCode());
            Assert.NotEqual(kernel0.GetHashCode(), kernel2.GetHashCode());
        }
    }
}
