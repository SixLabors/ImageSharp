// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Effects;

namespace SixLabors.ImageSharp.Tests.Processing.Effects;

[Trait("Category", "Processors")]
public class PixelateTest : BaseImageOperationsExtensionTest
{
    [Fact]
    public void Pixelate_PixelateProcessorDefaultsSet()
    {
        this.operations.Pixelate();
        PixelateProcessor processor = this.Verify<PixelateProcessor>();

        Assert.Equal(4, processor.Size);
    }

    [Fact]
    public void Pixelate_Size_PixelateProcessorDefaultsSet()
    {
        this.operations.Pixelate(12);
        PixelateProcessor processor = this.Verify<PixelateProcessor>();

        Assert.Equal(12, processor.Size);
    }

    [Fact]
    public void Pixelate_Size_rect_PixelateProcessorDefaultsSet()
    {
        this.operations.Pixelate(23, this.rect);
        PixelateProcessor processor = this.Verify<PixelateProcessor>(this.rect);

        Assert.Equal(23, processor.Size);
    }
}
