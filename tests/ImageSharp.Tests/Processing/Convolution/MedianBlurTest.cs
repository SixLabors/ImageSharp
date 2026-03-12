// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Convolution;

namespace SixLabors.ImageSharp.Tests.Processing.Convolution;

[Trait("Category", "Processors")]
public class MedianBlurTest : BaseImageOperationsExtensionTest
{
    [Fact]
    public void Median_radius_MedianProcessorDefaultsSet()
    {
        this.operations.MedianBlur(3, true);
        MedianBlurProcessor processor = this.Verify<MedianBlurProcessor>();

        Assert.Equal(3, processor.Radius);
        Assert.True(processor.PreserveAlpha);
    }

    [Fact]
    public void Median_radius_rect_MedianProcessorDefaultsSet()
    {
        this.operations.MedianBlur(this.rect, 5, false);
        MedianBlurProcessor processor = this.Verify<MedianBlurProcessor>(this.rect);

        Assert.Equal(5, processor.Radius);
        Assert.False(processor.PreserveAlpha);
    }
}
