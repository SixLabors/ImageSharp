// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    [Trait("Category", "Processors")]
    public class FlipTests : BaseImageOperationsExtensionTest
    {
        [Theory]
        [InlineData(FlipMode.None)]
        [InlineData(FlipMode.Horizontal)]
        [InlineData(FlipMode.Vertical)]
        public void Flip_degreesFloat_RotateProcessorWithAnglesSetAndExpandTrue(FlipMode flip)
        {
            this.operations.Flip(flip);
            FlipProcessor flipProcessor = this.Verify<FlipProcessor>();

            Assert.Equal(flip, flipProcessor.FlipMode);
        }
    }
}
