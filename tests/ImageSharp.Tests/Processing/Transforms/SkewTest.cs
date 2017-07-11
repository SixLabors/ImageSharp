// <copyright file="SkewTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Processing.Transforms
{
    using ImageSharp.PixelFormats;
    using ImageSharp.Processing.Processors;
    using Xunit;

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