// Copyright (c) Six Labors and contributors.
// Licensed under the GNU Affero General Public License, Version 3.

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
            SkewProcessor processor = this.Verify<SkewProcessor>();

            Assert.Equal(10, processor.DegreesX);
            Assert.Equal(20, processor.DegreesY);
        }
    }
}