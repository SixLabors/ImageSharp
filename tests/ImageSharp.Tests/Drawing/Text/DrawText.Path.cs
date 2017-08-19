// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Brushes;
using SixLabors.ImageSharp.Drawing.Pens;
using SixLabors.ImageSharp.Drawing.Processors;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Tests.Drawing.Paths;
using SixLabors.Fonts;
using SixLabors.Shapes;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing.Text
{
    public class DrawText_Path : BaseImageOperationsExtensionTest
    {
        Rgba32 color = Rgba32.HotPink;

        SolidBrush<Rgba32> brush = Brushes.Solid(Rgba32.HotPink);

        IPath path = new SixLabors.Shapes.Path(
            new LinearLineSegment(
                new SixLabors.Primitives.PointF[] { new Vector2(10, 10), new Vector2(20, 10), new Vector2(20, 10), new Vector2(30, 10), }));

        private readonly FontCollection FontCollection;

        private readonly Font Font;

        public DrawText_Path()
        {
            this.FontCollection = new FontCollection();
            this.Font = this.FontCollection.Install(TestFontUtilities.GetPath("SixLaborsSampleAB.woff")).CreateFont(12);
        }

        [Fact]
        public void FillsForEachACharachterWhenBrushSetAndNotPen()
        {
            this.operations.DrawText(
                "123",
                this.Font,
                Brushes.Solid(Rgba32.Red),
                null,
                path,
                new TextGraphicsOptions(true));

            this.Verify<FillRegionProcessor<Rgba32>>(0);
            this.Verify<FillRegionProcessor<Rgba32>>(1);
            this.Verify<FillRegionProcessor<Rgba32>>(2);
        }

        [Fact]
        public void FillsForEachACharachterWhenBrushSetAndNotPenDefaultOptions()
        {
            this.operations.DrawText("123", this.Font, Brushes.Solid(Rgba32.Red), null, path);

            this.Verify<FillRegionProcessor<Rgba32>>(0);
            this.Verify<FillRegionProcessor<Rgba32>>(1);
            this.Verify<FillRegionProcessor<Rgba32>>(2);
        }

        [Fact]
        public void FillsForEachACharachterWhenBrushSet()
        {
            this.operations.DrawText("123", this.Font, Brushes.Solid(Rgba32.Red), path, new TextGraphicsOptions(true));

            this.Verify<FillRegionProcessor<Rgba32>>(0);
            this.Verify<FillRegionProcessor<Rgba32>>(1);
            this.Verify<FillRegionProcessor<Rgba32>>(2);
        }

        [Fact]
        public void FillsForEachACharachterWhenBrushSetDefaultOptions()
        {
            this.operations.DrawText("123", this.Font, Brushes.Solid(Rgba32.Red), path);

            this.Verify<FillRegionProcessor<Rgba32>>(0);
            this.Verify<FillRegionProcessor<Rgba32>>(1);
            this.Verify<FillRegionProcessor<Rgba32>>(2);
        }

        [Fact]
        public void FillsForEachACharachterWhenColorSet()
        {
            this.operations.DrawText("123", this.Font, Rgba32.Red, path, new TextGraphicsOptions(true));

            var processor = this.Verify<FillRegionProcessor<Rgba32>>(0);
            this.Verify<FillRegionProcessor<Rgba32>>(1);
            this.Verify<FillRegionProcessor<Rgba32>>(2);

            SolidBrush<Rgba32> brush = Assert.IsType<SolidBrush<Rgba32>>(processor.Brush);
            Assert.Equal(Rgba32.Red, brush.Color);
        }

        [Fact]
        public void FillsForEachACharachterWhenColorSetDefaultOptions()
        {
            this.operations.DrawText("123", this.Font, Rgba32.Red, path);

            var processor = this.Verify<FillRegionProcessor<Rgba32>>(0);
            this.Verify<FillRegionProcessor<Rgba32>>(1);
            this.Verify<FillRegionProcessor<Rgba32>>(2);

            SolidBrush<Rgba32> brush = Assert.IsType<SolidBrush<Rgba32>>(processor.Brush);
            Assert.Equal(Rgba32.Red, brush.Color);
        }

        [Fact]
        public void DrawForEachACharachterWhenPenSetAndNotBrush()
        {
            this.operations.DrawText(
                "123",
                this.Font,
                null,
                Pens.Dash(Rgba32.Red, 1),
                path,
                new TextGraphicsOptions(true));

            var processor = this.Verify<FillRegionProcessor<Rgba32>>(0);
            this.Verify<FillRegionProcessor<Rgba32>>(1);
            this.Verify<FillRegionProcessor<Rgba32>>(2);
        }

        [Fact]
        public void DrawForEachACharachterWhenPenSetAndNotBrushDefaultOptions()
        {
            this.operations.DrawText("123", this.Font, null, Pens.Dash(Rgba32.Red, 1), path);

            var processor = this.Verify<FillRegionProcessor<Rgba32>>(0);
            this.Verify<FillRegionProcessor<Rgba32>>(1);
            this.Verify<FillRegionProcessor<Rgba32>>(2);
        }

        [Fact]
        public void DrawForEachACharachterWhenPenSet()
        {
            this.operations.DrawText("123", this.Font, Pens.Dash(Rgba32.Red, 1), path, new TextGraphicsOptions(true));

            var processor = this.Verify<FillRegionProcessor<Rgba32>>(0);
            this.Verify<FillRegionProcessor<Rgba32>>(1);
            this.Verify<FillRegionProcessor<Rgba32>>(2);
        }

        [Fact]
        public void DrawForEachACharachterWhenPenSetDefaultOptions()
        {
            this.operations.DrawText("123", this.Font, Pens.Dash(Rgba32.Red, 1), path);

            var processor = this.Verify<FillRegionProcessor<Rgba32>>(0);
            this.Verify<FillRegionProcessor<Rgba32>>(1);
            this.Verify<FillRegionProcessor<Rgba32>>(2);
        }

        [Fact]
        public void DrawForEachACharachterWhenPenSetAndFillFroEachWhenBrushSet()
        {
            this.operations.DrawText(
                "123",
                this.Font,
                Brushes.Solid(Rgba32.Red),
                Pens.Dash(Rgba32.Red, 1),
                path,
                new TextGraphicsOptions(true));

            var processor = this.Verify<FillRegionProcessor<Rgba32>>(0);
            this.Verify<FillRegionProcessor<Rgba32>>(1);
            this.Verify<FillRegionProcessor<Rgba32>>(2);
            this.Verify<FillRegionProcessor<Rgba32>>(3);
            this.Verify<FillRegionProcessor<Rgba32>>(4);
            this.Verify<FillRegionProcessor<Rgba32>>(5);
        }

        [Fact]
        public void DrawForEachACharachterWhenPenSetAndFillFroEachWhenBrushSetDefaultOptions()
        {
            this.operations.DrawText("123", this.Font, Brushes.Solid(Rgba32.Red), Pens.Dash(Rgba32.Red, 1), path);

            var processor = this.Verify<FillRegionProcessor<Rgba32>>(0);
            this.Verify<FillRegionProcessor<Rgba32>>(1);
            this.Verify<FillRegionProcessor<Rgba32>>(2);
            this.Verify<FillRegionProcessor<Rgba32>>(3);
            this.Verify<FillRegionProcessor<Rgba32>>(4);
            this.Verify<FillRegionProcessor<Rgba32>>(5);
        }

        [Fact]
        public void BrushAppliesBeforPen()
        {
            this.operations.DrawText(
                "1",
                this.Font,
                Brushes.Solid(Rgba32.Red),
                Pens.Dash(Rgba32.Red, 1),
                path,
            new TextGraphicsOptions(true));

            var processor = this.Verify<FillRegionProcessor<Rgba32>>(0);
            this.Verify<FillRegionProcessor<Rgba32>>(1);
        }

        [Fact]
        public void BrushAppliesBeforPenDefaultOptions()
        {
            this.operations.DrawText("1", this.Font, Brushes.Solid(Rgba32.Red), Pens.Dash(Rgba32.Red, 1), path);

            var processor = this.Verify<FillRegionProcessor<Rgba32>>(0);
            this.Verify<FillRegionProcessor<Rgba32>>(1);
        }
    }
}
