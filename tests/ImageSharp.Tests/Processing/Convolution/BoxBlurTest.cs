// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Convolution;

namespace SixLabors.ImageSharp.Tests.Processing.Convolution;

[Trait("Category", "Processors")]
public class BoxBlurTest : BaseImageOperationsExtensionTest
{
    [Fact]
    public void BoxBlur_BoxBlurProcessorDefaultsSet()
    {
        this.operations.BoxBlur();
        BoxBlurProcessor processor = this.Verify<BoxBlurProcessor>();

        Assert.Equal(7, processor.Radius);
    }

    [Fact]
    public void BoxBlur_amount_BoxBlurProcessorDefaultsSet()
    {
        this.operations.BoxBlur(34);
        BoxBlurProcessor processor = this.Verify<BoxBlurProcessor>();

        Assert.Equal(34, processor.Radius);
    }

    [Fact]
    public void BoxBlur_amount_rect_BoxBlurProcessorDefaultsSet()
    {
        this.operations.BoxBlur(5, this.rect);
        BoxBlurProcessor processor = this.Verify<BoxBlurProcessor>(this.rect);

        Assert.Equal(5, processor.Radius);
    }
}
