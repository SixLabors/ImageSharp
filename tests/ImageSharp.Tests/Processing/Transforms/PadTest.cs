// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Transforms.Resamplers;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    using SixLabors.ImageSharp.Processing.Transforms;
    using SixLabors.ImageSharp.Processing.Transforms.Processors;

    public class PadTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void PadWidthHeightResizeProcessorWithCorrectOptionsSet()
        {
            int width = 500;
            int height = 565;
            IResampler sampler = KnownResamplers.NearestNeighbor;

            this.operations.Pad(width, height);
            ResizeProcessor<Rgba32> resizeProcessor = this.Verify<ResizeProcessor<Rgba32>>();

            Assert.Equal(width, resizeProcessor.Width);
            Assert.Equal(height, resizeProcessor.Height);
            Assert.Equal(sampler, resizeProcessor.Sampler);
        }
    }
}