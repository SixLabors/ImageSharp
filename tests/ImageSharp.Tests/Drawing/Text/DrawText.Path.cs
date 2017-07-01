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
    using ImageSharp.PixelFormats;
    using ImageSharp.Tests.Drawing.Paths;

    using SixLabors.Fonts;
    using SixLabors.Shapes;

    using Xunit;

    public class DrawText_Path : IDisposable
    {
        Rgba32 color = Rgba32.HotPink;

        SolidBrush<Rgba32> brush = Brushes.Solid(Rgba32.HotPink);

        IPath path = new SixLabors.Shapes.Path(
            new LinearLineSegment(
                new SixLabors.Primitives.PointF[] { new Vector2(10, 10), new Vector2(20, 10), new Vector2(20, 10), new Vector2(30, 10), }));

        private ProcessorWatchingImage img;

        private readonly FontCollection FontCollection;

        private readonly Font Font;

        public DrawText_Path()
        {
            this.FontCollection = new FontCollection();
            this.Font = this.FontCollection.Install(TestFontUtilities.GetPath("SixLaborsSampleAB.woff")).CreateFont(12);
            this.img = new ProcessorWatchingImage(10, 10);
        }

        public void Dispose()
        {
            this.img.Dispose();
        }

        [Fact]
        public void FillsForEachACharachterWhenBrushSetAndNotPen()
        {
            this.img.Mutate(x => x.DrawText(
                "123",
                this.Font,
                Brushes.Solid(Rgba32.Red),
                null,
                path,
                new TextGraphicsOptions(true)));

            Assert.NotEmpty(this.img.ProcessorApplications);
            Assert.Equal(3, this.img.ProcessorApplications.Count); // 3 fills where applied
            Assert.IsType<FillRegionProcessor<Rgba32>>(this.img.ProcessorApplications[0].processor);
        }

        [Fact]
        public void FillsForEachACharachterWhenBrushSetAndNotPenDefaultOptions()
        {
            this.img.Mutate(x => x.DrawText("123", this.Font, Brushes.Solid(Rgba32.Red), null, path));

            Assert.NotEmpty(this.img.ProcessorApplications);
            Assert.Equal(3, this.img.ProcessorApplications.Count); // 3 fills where applied
            Assert.IsType<FillRegionProcessor<Rgba32>>(this.img.ProcessorApplications[0].processor);
        }

        [Fact]
        public void FillsForEachACharachterWhenBrushSet()
        {
            this.img.Mutate(x => x.DrawText("123", this.Font, Brushes.Solid(Rgba32.Red), path, new TextGraphicsOptions(true)));

            Assert.NotEmpty(this.img.ProcessorApplications);
            Assert.Equal(3, this.img.ProcessorApplications.Count); // 3 fills where applied
            Assert.IsType<FillRegionProcessor<Rgba32>>(this.img.ProcessorApplications[0].processor);
        }

        [Fact]
        public void FillsForEachACharachterWhenBrushSetDefaultOptions()
        {
            this.img.Mutate(x => x.DrawText("123", this.Font, Brushes.Solid(Rgba32.Red), path));

            Assert.NotEmpty(this.img.ProcessorApplications);
            Assert.Equal(3, this.img.ProcessorApplications.Count); // 3 fills where applied
            Assert.IsType<FillRegionProcessor<Rgba32>>(this.img.ProcessorApplications[0].processor);
        }

        [Fact]
        public void FillsForEachACharachterWhenColorSet()
        {
            this.img.Mutate(x => x.DrawText("123", this.Font, Rgba32.Red, path, new TextGraphicsOptions(true)));

            Assert.NotEmpty(this.img.ProcessorApplications);
            Assert.Equal(3, this.img.ProcessorApplications.Count);
            FillRegionProcessor<Rgba32> processor =
                Assert.IsType<FillRegionProcessor<Rgba32>>(this.img.ProcessorApplications[0].processor);

            SolidBrush<Rgba32> brush = Assert.IsType<SolidBrush<Rgba32>>(processor.Brush);
            Assert.Equal(Rgba32.Red, brush.Color);
        }

        [Fact]
        public void FillsForEachACharachterWhenColorSetDefaultOptions()
        {
            this.img.Mutate(x => x.DrawText("123", this.Font, Rgba32.Red, path));

            Assert.NotEmpty(this.img.ProcessorApplications);
            Assert.Equal(3, this.img.ProcessorApplications.Count);
            Assert.IsType<FillRegionProcessor<Rgba32>>(this.img.ProcessorApplications[0].processor);
            FillRegionProcessor<Rgba32> processor =
                Assert.IsType<FillRegionProcessor<Rgba32>>(this.img.ProcessorApplications[0].processor);

            SolidBrush<Rgba32> brush = Assert.IsType<SolidBrush<Rgba32>>(processor.Brush);
            Assert.Equal(Rgba32.Red, brush.Color);
        }

        [Fact]
        public void DrawForEachACharachterWhenPenSetAndNotBrush()
        {
            this.img.Mutate(x => x.DrawText(
                "123",
                this.Font,
                null,
                Pens.Dash(Rgba32.Red, 1),
                path,
                new TextGraphicsOptions(true)));

            Assert.NotEmpty(this.img.ProcessorApplications);
            Assert.Equal(3, this.img.ProcessorApplications.Count); // 3 fills where applied
            Assert.IsType<FillRegionProcessor<Rgba32>>(this.img.ProcessorApplications[0].processor);
        }

        [Fact]
        public void DrawForEachACharachterWhenPenSetAndNotBrushDefaultOptions()
        {
            this.img.Mutate(x => x.DrawText("123", this.Font, null, Pens.Dash(Rgba32.Red, 1), path));

            Assert.NotEmpty(this.img.ProcessorApplications);
            Assert.Equal(3, this.img.ProcessorApplications.Count); // 3 fills where applied
            Assert.IsType<FillRegionProcessor<Rgba32>>(this.img.ProcessorApplications[0].processor);
        }

        [Fact]
        public void DrawForEachACharachterWhenPenSet()
        {
            this.img.Mutate(x => x.DrawText("123", this.Font, Pens.Dash(Rgba32.Red, 1), path, new TextGraphicsOptions(true)));

            Assert.NotEmpty(this.img.ProcessorApplications);
            Assert.Equal(3, this.img.ProcessorApplications.Count); // 3 fills where applied
            Assert.IsType<FillRegionProcessor<Rgba32>>(this.img.ProcessorApplications[0].processor);
        }

        [Fact]
        public void DrawForEachACharachterWhenPenSetDefaultOptions()
        {
            this.img.Mutate(x => x.DrawText("123", this.Font, Pens.Dash(Rgba32.Red, 1), path));

            Assert.NotEmpty(this.img.ProcessorApplications);
            Assert.Equal(3, this.img.ProcessorApplications.Count); // 3 fills where applied
            Assert.IsType<FillRegionProcessor<Rgba32>>(this.img.ProcessorApplications[0].processor);
        }

        [Fact]
        public void DrawForEachACharachterWhenPenSetAndFillFroEachWhenBrushSet()
        {
            this.img.Mutate(x => x.DrawText(
                "123",
                this.Font,
                Brushes.Solid(Rgba32.Red),
                Pens.Dash(Rgba32.Red, 1),
                path,
                new TextGraphicsOptions(true)));

            Assert.NotEmpty(this.img.ProcessorApplications);
            Assert.Equal(6, this.img.ProcessorApplications.Count);
        }

        [Fact]
        public void DrawForEachACharachterWhenPenSetAndFillFroEachWhenBrushSetDefaultOptions()
        {
            this.img.Mutate(x => x.DrawText("123", this.Font, Brushes.Solid(Rgba32.Red), Pens.Dash(Rgba32.Red, 1), path));

            Assert.NotEmpty(this.img.ProcessorApplications);
            Assert.Equal(6, this.img.ProcessorApplications.Count);
        }

        [Fact]
        public void BrushAppliesBeforPen()
        {
            this.img.Mutate(x => x.DrawText(
                "1",
                this.Font,
                Brushes.Solid(Rgba32.Red),
                Pens.Dash(Rgba32.Red, 1),
                path,
            new TextGraphicsOptions(true)));

            Assert.NotEmpty(this.img.ProcessorApplications);
            Assert.Equal(2, this.img.ProcessorApplications.Count);
            Assert.IsType<FillRegionProcessor<Rgba32>>(this.img.ProcessorApplications[0].processor);
            Assert.IsType<FillRegionProcessor<Rgba32>>(this.img.ProcessorApplications[1].processor);
        }

        [Fact]
        public void BrushAppliesBeforPenDefaultOptions()
        {
            this.img.Mutate(x => x.DrawText("1", this.Font, Brushes.Solid(Rgba32.Red), Pens.Dash(Rgba32.Red, 1), path));

            Assert.NotEmpty(this.img.ProcessorApplications);
            Assert.Equal(2, this.img.ProcessorApplications.Count);
            Assert.IsType<FillRegionProcessor<Rgba32>>(this.img.ProcessorApplications[0].processor);
            Assert.IsType<FillRegionProcessor<Rgba32>>(this.img.ProcessorApplications[1].processor);
        }
    }
}
