// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    public class PadTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void PadWidthHeightResizeProcessorWithCorrectOptionsSet()
        {
            int width = 500;
            int height = 565;
            IResampler sampler = KnownResamplers.NearestNeighbor;

            this.operations.Pad(width, height);
            ResizeProcessor resizeProcessor = this.Verify<ResizeProcessor>();

            Assert.Equal(width, resizeProcessor.DestinationWidth);
            Assert.Equal(height, resizeProcessor.DestinationHeight);
            Assert.Equal(sampler, resizeProcessor.Sampler);
        }
    }
}
