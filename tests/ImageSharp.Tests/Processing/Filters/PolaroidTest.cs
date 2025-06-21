// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Filters;

namespace SixLabors.ImageSharp.Tests.Processing.Filters;

[Trait("Category", "Processors")]
public class PolaroidTest : BaseImageOperationsExtensionTest
{
    [Fact]
    public void Polaroid_amount_PolaroidProcessorDefaultsSet()
    {
        this.operations.Polaroid();
        PolaroidProcessor processor = this.Verify<PolaroidProcessor>();
        Assert.Equal(processor.GraphicsOptions, this.options);
    }

    [Fact]
    public void Polaroid_amount_rect_PolaroidProcessorDefaultsSet()
    {
        this.operations.Polaroid(this.rect);
        PolaroidProcessor processor = this.Verify<PolaroidProcessor>(this.rect);
        Assert.Equal(processor.GraphicsOptions, this.options);
    }
}
