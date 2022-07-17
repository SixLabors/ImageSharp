// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Transforms
{
    [Trait("Category", "Processors")]
    public class AutoOrientTests : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void AutoOrient_AutoOrientProcessor()
        {
            this.operations.AutoOrient();
            this.Verify<AutoOrientProcessor>();
        }
    }
}
