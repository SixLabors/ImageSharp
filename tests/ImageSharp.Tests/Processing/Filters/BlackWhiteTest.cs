// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Filters;

namespace SixLabors.ImageSharp.Tests.Processing.Filters;

[Trait("Category", "Processors")]
public class BlackWhiteTest : BaseImageOperationsExtensionTest
{
    [Fact]
    public void BlackWhite_CorrectProcessor()
    {
        this.operations.BlackWhite();
        this.Verify<BlackWhiteProcessor>();
    }

    [Fact]
    public void BlackWhite_rect_CorrectProcessor()
    {
        this.operations.BlackWhite(this.rect);
        this.Verify<BlackWhiteProcessor>(this.rect);
    }
}
