// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Processors;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    public class SkewTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void Skew_x_y_CreateSkewProcessorWithAnglesSetAndExpandTrue()
        {
            this.operations.Skew(10, 20);
            var processor = this.Verify<SkewProcessor<Rgba32>>();

            Assert.Equal(10, processor.AngleX);
            Assert.Equal(20, processor.AngleY);
            Assert.True(processor.Expand);
        }

        [Fact]
        public void Skew_x_y_expand_CreateSkewProcessorWithAnglesSetAndExpandTrue()
        {
            this.operations.Skew(10, 20, false);
            var processor = this.Verify<SkewProcessor<Rgba32>>();

            Assert.Equal(10, processor.AngleX);
            Assert.Equal(20, processor.AngleY);
            Assert.False(processor.Expand);
        }
    }
}