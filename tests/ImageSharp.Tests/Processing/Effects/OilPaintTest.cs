// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Effects;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Effects
{
    [Trait("Category", "Processors")]
    public class OilPaintTest : BaseImageOperationsExtensionTest
    {
        [Fact]
        public void OilPaint_OilPaintingProcessorDefaultsSet()
        {
            this.operations.OilPaint();
            var processor = this.Verify<OilPaintingProcessor>();

            Assert.Equal(10, processor.Levels);
            Assert.Equal(15, processor.BrushSize);
        }

        [Fact]
        public void OilPaint_rect_OilPaintingProcessorDefaultsSet()
        {
            this.operations.OilPaint(this.rect);
            var processor = this.Verify<OilPaintingProcessor>(this.rect);

            Assert.Equal(10, processor.Levels);
            Assert.Equal(15, processor.BrushSize);
        }

        [Fact]
        public void OilPaint_Levels_Brush_OilPaintingProcessorDefaultsSet()
        {
            this.operations.OilPaint(34, 65);
            var processor = this.Verify<OilPaintingProcessor>();

            Assert.Equal(34, processor.Levels);
            Assert.Equal(65, processor.BrushSize);
        }

        [Fact]
        public void OilPaint_Levels_Brush_rect_OilPaintingProcessorDefaultsSet()
        {
            this.operations.OilPaint(54, 43, this.rect);
            var processor = this.Verify<OilPaintingProcessor>(this.rect);

            Assert.Equal(54, processor.Levels);
            Assert.Equal(43, processor.BrushSize);
        }
    }
}
