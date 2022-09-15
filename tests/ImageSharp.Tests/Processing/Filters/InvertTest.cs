// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Filters;

namespace SixLabors.ImageSharp.Tests.Processing.Filters;

[Trait("Category", "Processors")]
public class InvertTest : BaseImageOperationsExtensionTest
{
    [Fact]
    public void Invert_InvertProcessorDefaultsSet()
    {
        this.operations.Invert();
        this.Verify<InvertProcessor>();
    }

    [Fact]
    public void Pixelate_rect_PixelateProcessorDefaultsSet()
    {
        this.operations.Invert(this.rect);
        this.Verify<InvertProcessor>(this.rect);
    }
}
