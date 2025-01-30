// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Effects;

namespace SixLabors.ImageSharp.Tests.Processing.Effects;

[Trait("Category", "Processors")]
public class OilPaintTest : BaseImageOperationsExtensionTest
{
    [Fact]
    public void OilPaint_OilPaintingProcessorDefaultsSet()
    {
        this.operations.OilPaint();
        OilPaintingProcessor processor = this.Verify<OilPaintingProcessor>();

        Assert.Equal(10, processor.Levels);
        Assert.Equal(15, processor.BrushSize);
    }

    [Fact]
    public void OilPaint_rect_OilPaintingProcessorDefaultsSet()
    {
        this.operations.OilPaint(this.rect);
        OilPaintingProcessor processor = this.Verify<OilPaintingProcessor>(this.rect);

        Assert.Equal(10, processor.Levels);
        Assert.Equal(15, processor.BrushSize);
    }

    [Fact]
    public void OilPaint_Levels_Brush_OilPaintingProcessorDefaultsSet()
    {
        this.operations.OilPaint(34, 65);
        OilPaintingProcessor processor = this.Verify<OilPaintingProcessor>();

        Assert.Equal(34, processor.Levels);
        Assert.Equal(65, processor.BrushSize);
    }

    [Fact]
    public void OilPaint_Levels_Brush_rect_OilPaintingProcessorDefaultsSet()
    {
        this.operations.OilPaint(54, 43, this.rect);
        OilPaintingProcessor processor = this.Verify<OilPaintingProcessor>(this.rect);

        Assert.Equal(54, processor.Levels);
        Assert.Equal(43, processor.BrushSize);
    }
}
