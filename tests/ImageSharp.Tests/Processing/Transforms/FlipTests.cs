// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    using SixLabors.ImageSharp.Processing;
    using SixLabors.ImageSharp.Processing.Processors.Transforms;

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
