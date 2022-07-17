// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    [Trait("Category", "Processors")]
    public class SkewTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void SkewXYCreateSkewProcessorWithAnglesSet()
        {
            this.operations.Skew(10, 20);
            SkewProcessor processor = this.Verify<SkewProcessor>();

            Assert.Equal(10, processor.DegreesX);
            Assert.Equal(20, processor.DegreesY);
        }
    }
}
