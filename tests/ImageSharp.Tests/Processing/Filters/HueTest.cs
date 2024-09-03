// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Filters;

namespace SixLabors.ImageSharp.Tests.Processing.Filters;

[Trait("Category", "Processors")]
public class HueTest : BaseImageOperationsExtensionTest
{
    [Fact]
    public void Hue_amount_HueProcessorDefaultsSet()
    {
        this.operations.Hue(34f);
        HueProcessor processor = this.Verify<HueProcessor>();

        Assert.Equal(34f, processor.Degrees);
    }

    [Fact]
    public void Hue_amount_rect_HueProcessorDefaultsSet()
    {
        this.operations.Hue(5f, this.rect);
        HueProcessor processor = this.Verify<HueProcessor>(this.rect);

        Assert.Equal(5f, processor.Degrees);
    }
}
