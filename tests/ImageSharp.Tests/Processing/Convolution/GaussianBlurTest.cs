// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Convolution;

namespace SixLabors.ImageSharp.Tests.Processing.Convolution;

[Trait("Category", "Processors")]
public class GaussianBlurTest : BaseImageOperationsExtensionTest
{
    [Fact]
    public void GaussianBlur_GaussianBlurProcessorDefaultsSet()
    {
        this.operations.GaussianBlur();
        GaussianBlurProcessor processor = this.Verify<GaussianBlurProcessor>();

        Assert.Equal(3f, processor.Sigma);
    }

    [Fact]
    public void GaussianBlur_amount_GaussianBlurProcessorDefaultsSet()
    {
        this.operations.GaussianBlur(0.2f);
        GaussianBlurProcessor processor = this.Verify<GaussianBlurProcessor>();

        Assert.Equal(.2f, processor.Sigma);
    }

    [Fact]
    public void GaussianBlur_amount_rect_GaussianBlurProcessorDefaultsSet()
    {
        this.operations.GaussianBlur(this.rect, 0.6f);
        GaussianBlurProcessor processor = this.Verify<GaussianBlurProcessor>(this.rect);

        Assert.Equal(.6f, processor.Sigma);
    }
}
