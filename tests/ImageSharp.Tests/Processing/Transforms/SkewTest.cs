// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    public class SkewTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void SkewXYCreateSkewProcessorWithAnglesSet()
        {
            this.operations.Skew(10, 20);
            SkewProcessor<Rgba32> processor = this.Verify<SkewProcessor<Rgba32>>();

            Assert.Equal(10, processor.DegreesX);
            Assert.Equal(20, processor.DegreesY);
        }
    }
}