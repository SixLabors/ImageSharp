// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.Fonts;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Text;
using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing.Text
{
    public class DrawText : BaseImageOperationsExtensionTest
    {
        private readonly FontCollection FontCollection;

        private readonly Font Font;

        public DrawText()
        {
            this.FontCollection = new FontCollection();
            this.Font = this.FontCollection.Install(TestFontUtilities.GetPath("SixLaborsSampleAB.woff")).CreateFont(12);
        }

        [Fact]
        public void FillsForEachACharacterWhenBrushSetAndNotPen()
        {
            this.operations.DrawText(
                new TextGraphicsOptions(true),
                "123",
                this.Font,
                Brushes.Solid(Color.Red),
                null,
                Vector2.Zero);

            this.Verify<DrawTextProcessor>(0);
        }

        [Fact]
        public void FillsForEachACharacterWhenBrushSetAndNotPenDefaultOptions()
        {
            this.operations.DrawText("123", this.Font, Brushes.Solid(Color.Red), null, Vector2.Zero);

            this.Verify<DrawTextProcessor>(0);
        }

        [Fact]
        public void FillsForEachACharacterWhenBrushSet()
        {
            this.operations.DrawText(new TextGraphicsOptions(true), "123", this.Font, Brushes.Solid(Color.Red), Vector2.Zero);

            this.Verify<DrawTextProcessor>(0);
        }

        [Fact]
        public void FillsForEachACharacterWhenBrushSetDefaultOptions()
        {
            this.operations.DrawText("123", this.Font, Brushes.Solid(Color.Red), Vector2.Zero);

            this.Verify<DrawTextProcessor>(0);
        }

        [Fact]
        public void FillsForEachACharacterWhenColorSet()
        {
            this.operations.DrawText(new TextGraphicsOptions(true), "123", this.Font, Color.Red, Vector2.Zero);

            var processor = this.Verify<DrawTextProcessor>(0);

            SolidBrush brush = Assert.IsType<SolidBrush>(processor.Brush);
            Assert.Equal(Color.Red, brush.Color);
        }

        [Fact]
        public void FillsForEachACharacterWhenColorSetDefaultOptions()
        {
            this.operations.DrawText("123", this.Font, Color.Red, Vector2.Zero);

            var processor = this.Verify<DrawTextProcessor>(0);

            SolidBrush brush = Assert.IsType<SolidBrush>(processor.Brush);
            Assert.Equal(Color.Red, brush.Color);
        }

        [Fact]
        public void DrawForEachACharacterWhenPenSetAndNotBrush()
        {
            this.operations.DrawText(
                new TextGraphicsOptions(true),
                "123",
                this.Font,
                null,
                Pens.Dash(Color.Red, 1),
                Vector2.Zero);

            this.Verify<DrawTextProcessor>(0);
        }

        [Fact]
        public void DrawForEachACharacterWhenPenSetAndNotBrushDefaultOptions()
        {
            this.operations.DrawText("123", this.Font, null, Pens.Dash(Color.Red, 1), Vector2.Zero);

            this.Verify<DrawTextProcessor>(0);
        }

        [Fact]
        public void DrawForEachACharacterWhenPenSet()
        {
            this.operations.DrawText(new TextGraphicsOptions(true), "123", this.Font, Pens.Dash(Color.Red, 1), Vector2.Zero);

            this.Verify<DrawTextProcessor>(0);
        }

        [Fact]
        public void DrawForEachACharacterWhenPenSetDefaultOptions()
        {
            this.operations.DrawText("123", this.Font, Pens.Dash(Color.Red, 1), Vector2.Zero);

            var processor = this.Verify<DrawTextProcessor>(0);

            Assert.Equal("123", processor.Text);
            Assert.Equal(this.Font, processor.Font);
            var penBrush = Assert.IsType<SolidBrush>(processor.Pen.StrokeFill);
            Assert.Equal(Color.Red, penBrush.Color);
            Assert.Equal(1, processor.Pen.StrokeWidth);
            Assert.Equal(PointF.Empty, processor.Location);
        }

        [Fact]
        public void DrawForEachACharacterWhenPenSetAndFillFroEachWhenBrushSet()
        {
            this.operations.DrawText(
                new TextGraphicsOptions(true),
                "123",
                this.Font,
                Brushes.Solid(Color.Red),
                Pens.Dash(Color.Red, 1),
                Vector2.Zero);

            var processor = this.Verify<DrawTextProcessor>(0);

            Assert.Equal("123", processor.Text);
            Assert.Equal(this.Font, processor.Font);
            var brush = Assert.IsType<SolidBrush>(processor.Brush);
            Assert.Equal(Color.Red, brush.Color);
            Assert.Equal(PointF.Empty, processor.Location);
            var penBrush = Assert.IsType<SolidBrush>(processor.Pen.StrokeFill);
            Assert.Equal(Color.Red, penBrush.Color);
            Assert.Equal(1, processor.Pen.StrokeWidth);
        }
    }
}
