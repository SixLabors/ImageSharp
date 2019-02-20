// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Drawing;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing.Paths
{
    public class FillRectangle : BaseImageOperationsExtensionTest
    {
        private readonly GraphicsOptions noneDefault = new GraphicsOptions();
        private readonly Rgba32 colorForTesting = Rgba32.HotPink;
        private readonly SolidBrush<Rgba32> brushForTesting = Brushes.Solid(Rgba32.HotPink);
        private readonly SixLabors.Primitives.Rectangle rectangleForTesting = new SixLabors.Primitives.Rectangle(10, 10, 77, 76);

        [Fact]
        public void CorrectlySetsBrushAndRectangle()
        {
            this.operations.Fill(this.brushForTesting, this.rectangleForTesting);
            FillProcessor<Rgba32> processor = this.Verify<FillProcessor<Rgba32>>();
            Assert.Equal(GraphicsOptions.Default, processor.Options);
            Assert.Equal(this.brushForTesting, processor.Brush);
        }

        [Fact]
        public void CorrectlySetsBrushRectangleAndOptions()
        {
            this.operations.Fill(this.noneDefault, this.brushForTesting, this.rectangleForTesting);
            FillProcessor<Rgba32> processor = this.Verify<FillProcessor<Rgba32>>();
            Assert.Equal(this.noneDefault, processor.Options);
            Assert.Equal(this.brushForTesting, processor.Brush);
        }

        [Fact]
        public void CorrectlySetsColorAndRectangle()
        {
            this.operations.Fill(this.colorForTesting, this.rectangleForTesting);
            FillProcessor<Rgba32> processor = this.Verify<FillProcessor<Rgba32>>();
            Assert.Equal(GraphicsOptions.Default, processor.Options);
            SolidBrush<Rgba32> brush = Assert.IsType<SolidBrush<Rgba32>>(processor.Brush);
            Assert.Equal(this.colorForTesting, brush.Color);
        }

        [Fact]
        public void CorrectlySetsColorRectangleAndOptions()
        {
            this.operations.Fill(this.noneDefault, this.colorForTesting, this.rectangleForTesting);
            FillProcessor<Rgba32> processor = this.Verify<FillProcessor<Rgba32>>();
            Assert.Equal(this.noneDefault, processor.Options);
            SolidBrush<Rgba32> brush = Assert.IsType<SolidBrush<Rgba32>>(processor.Brush);
            Assert.Equal(this.colorForTesting, brush.Color);
        }
    }
}
