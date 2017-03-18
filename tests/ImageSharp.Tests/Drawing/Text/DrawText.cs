// <copyright file="DrawText.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Drawing.Text
{
    using System;
    using System.Numerics;

    using ImageSharp.Drawing;
    using ImageSharp.Drawing.Brushes;
    using ImageSharp.Drawing.Pens;
    using ImageSharp.Drawing.Processors;
    using ImageSharp.Tests.Drawing.Paths;

    using SixLabors.Fonts;
    using SixLabors.Shapes;

    using Xunit;

    public class DrawText : IDisposable
    {
        Color color = Color.HotPink;

        SolidBrush brush = Brushes.Solid(Color.HotPink);

        IPath path = new SixLabors.Shapes.Path(
            new LinearLineSegment(
                new Vector2[] { new Vector2(10, 10), new Vector2(20, 10), new Vector2(20, 10), new Vector2(30, 10), }));

        private ProcessorWatchingImage img;

        private readonly FontCollection FontCollection;

        private readonly Font Font;

        public DrawText()
        {
            this.FontCollection = new FontCollection();
            this.Font = this.FontCollection.Install(TestFontUtilities.GetPath("SixLaborsSampleAB.woff"));
            this.img = new ProcessorWatchingImage(10, 10);
        }

        public void Dispose()
        {
            this.img.Dispose();
        }

        [Fact]
        public void FillsForEachACharachterWhenBrushSetAndNotPen()
        {
            this.img.DrawText(
                "123",
                this.Font,
                Brushes.Solid(Color.Red),
                null,
                Vector2.Zero,
                new TextGraphicsOptions(true));

            Assert.NotEmpty(this.img.ProcessorApplications);
            Assert.Equal(3, this.img.ProcessorApplications.Count); // 3 fills where applied
            Assert.IsType<FillRegionProcessor<Color>>(this.img.ProcessorApplications[0].processor);
        }

        [Fact]
        public void FillsForEachACharachterWhenBrushSetAndNotPenDefaultOptions()
        {
            this.img.DrawText("123", this.Font, Brushes.Solid(Color.Red), null, Vector2.Zero);

            Assert.NotEmpty(this.img.ProcessorApplications);
            Assert.Equal(3, this.img.ProcessorApplications.Count); // 3 fills where applied
            Assert.IsType<FillRegionProcessor<Color>>(this.img.ProcessorApplications[0].processor);
        }

        [Fact]
        public void FillsForEachACharachterWhenBrushSet()
        {
            this.img.DrawText("123", this.Font, Brushes.Solid(Color.Red), Vector2.Zero, new TextGraphicsOptions(true));

            Assert.NotEmpty(this.img.ProcessorApplications);
            Assert.Equal(3, this.img.ProcessorApplications.Count); // 3 fills where applied
            Assert.IsType<FillRegionProcessor<Color>>(this.img.ProcessorApplications[0].processor);
        }

        [Fact]
        public void FillsForEachACharachterWhenBrushSetDefaultOptions()
        {
            this.img.DrawText("123", this.Font, Brushes.Solid(Color.Red), Vector2.Zero);

            Assert.NotEmpty(this.img.ProcessorApplications);
            Assert.Equal(3, this.img.ProcessorApplications.Count); // 3 fills where applied
            Assert.IsType<FillRegionProcessor<Color>>(this.img.ProcessorApplications[0].processor);
        }

        [Fact]
        public void FillsForEachACharachterWhenColorSet()
        {
            this.img.DrawText("123", this.Font, Color.Red, Vector2.Zero, new TextGraphicsOptions(true));

            Assert.NotEmpty(this.img.ProcessorApplications);
            Assert.Equal(3, this.img.ProcessorApplications.Count);
            FillRegionProcessor<Color> processor =
                Assert.IsType<FillRegionProcessor<Color>>(this.img.ProcessorApplications[0].processor);

            SolidBrush<Color> brush = Assert.IsType<SolidBrush<Color>>(processor.Brush);
            Assert.Equal(Color.Red, brush.Color);
        }

        [Fact]
        public void FillsForEachACharachterWhenColorSetDefaultOptions()
        {
            this.img.DrawText("123", this.Font, Color.Red, Vector2.Zero);

            Assert.NotEmpty(this.img.ProcessorApplications);
            Assert.Equal(3, this.img.ProcessorApplications.Count);
            Assert.IsType<FillRegionProcessor<Color>>(this.img.ProcessorApplications[0].processor);
            FillRegionProcessor<Color> processor =
                Assert.IsType<FillRegionProcessor<Color>>(this.img.ProcessorApplications[0].processor);

            SolidBrush<Color> brush = Assert.IsType<SolidBrush<Color>>(processor.Brush);
            Assert.Equal(Color.Red, brush.Color);
        }

        [Fact]
        public void DrawForEachACharachterWhenPenSetAndNotBrush()
        {
            this.img.DrawText(
                "123",
                this.Font,
                null,
                Pens.Dash(Color.Red, 1),
                Vector2.Zero,
                new TextGraphicsOptions(true));

            Assert.NotEmpty(this.img.ProcessorApplications);
            Assert.Equal(3, this.img.ProcessorApplications.Count); // 3 fills where applied
            Assert.IsType<DrawPathProcessor<Color>>(this.img.ProcessorApplications[0].processor);
        }

        [Fact]
        public void DrawForEachACharachterWhenPenSetAndNotBrushDefaultOptions()
        {
            this.img.DrawText("123", this.Font, null, Pens.Dash(Color.Red, 1), Vector2.Zero);

            Assert.NotEmpty(this.img.ProcessorApplications);
            Assert.Equal(3, this.img.ProcessorApplications.Count); // 3 fills where applied
            Assert.IsType<DrawPathProcessor<Color>>(this.img.ProcessorApplications[0].processor);
        }

        [Fact]
        public void DrawForEachACharachterWhenPenSet()
        {
            this.img.DrawText("123", this.Font, Pens.Dash(Color.Red, 1), Vector2.Zero, new TextGraphicsOptions(true));

            Assert.NotEmpty(this.img.ProcessorApplications);
            Assert.Equal(3, this.img.ProcessorApplications.Count); // 3 fills where applied
            Assert.IsType<DrawPathProcessor<Color>>(this.img.ProcessorApplications[0].processor);
        }

        [Fact]
        public void DrawForEachACharachterWhenPenSetDefaultOptions()
        {
            this.img.DrawText("123", this.Font, Pens.Dash(Color.Red, 1), Vector2.Zero);

            Assert.NotEmpty(this.img.ProcessorApplications);
            Assert.Equal(3, this.img.ProcessorApplications.Count); // 3 fills where applied
            Assert.IsType<DrawPathProcessor<Color>>(this.img.ProcessorApplications[0].processor);
        }

        [Fact]
        public void DrawForEachACharachterWhenPenSetAndFillFroEachWhenBrushSet()
        {
            this.img.DrawText(
                "123",
                this.Font,
                Brushes.Solid(Color.Red),
                Pens.Dash(Color.Red, 1),
                Vector2.Zero,
                new TextGraphicsOptions(true));

            Assert.NotEmpty(this.img.ProcessorApplications);
            Assert.Equal(6, this.img.ProcessorApplications.Count);
        }

        [Fact]
        public void DrawForEachACharachterWhenPenSetAndFillFroEachWhenBrushSetDefaultOptions()
        {
            this.img.DrawText("123", this.Font, Brushes.Solid(Color.Red), Pens.Dash(Color.Red, 1), Vector2.Zero);

            Assert.NotEmpty(this.img.ProcessorApplications);
            Assert.Equal(6, this.img.ProcessorApplications.Count);
        }

        [Fact]
        public void BrushAppliesBeforPen()
        {
            this.img.DrawText(
                "1",
                this.Font,
                Brushes.Solid(Color.Red),
                Pens.Dash(Color.Red, 1),
                Vector2.Zero,
                new TextGraphicsOptions(true));

            Assert.NotEmpty(this.img.ProcessorApplications);
            Assert.Equal(2, this.img.ProcessorApplications.Count);
            Assert.IsType<FillRegionProcessor<Color>>(this.img.ProcessorApplications[0].processor);
            Assert.IsType<DrawPathProcessor<Color>>(this.img.ProcessorApplications[1].processor);
        }

        [Fact]
        public void BrushAppliesBeforPenDefaultOptions()
        {
            this.img.DrawText("1", this.Font, Brushes.Solid(Color.Red), Pens.Dash(Color.Red, 1), Vector2.Zero);

            Assert.NotEmpty(this.img.ProcessorApplications);
            Assert.Equal(2, this.img.ProcessorApplications.Count);
            Assert.IsType<FillRegionProcessor<Color>>(this.img.ProcessorApplications[0].processor);
            Assert.IsType<DrawPathProcessor<Color>>(this.img.ProcessorApplications[1].processor);
        }
    }
}
