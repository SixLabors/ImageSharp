
namespace ImageSharp.Tests.Drawing.Text
{
    using System;
    using System.IO;
    using ImageSharp;
    using ImageSharp.Drawing.Brushes;
    using Processing;
    using System.Collections.Generic;
    using Xunit;
    using ImageSharp.Drawing;
    using System.Numerics;
    using SixLabors.Shapes;
    using ImageSharp.Drawing.Processors;
    using ImageSharp.Drawing.Pens;
    using SixLabors.Fonts;
    using Paths;

    public class DrawText : IDisposable
    {
        Color color = Color.HotPink;
        SolidBrush brush = Brushes.Solid(Color.HotPink);
        IPath path = new SixLabors.Shapes.Path(new LinearLineSegment(new Vector2[] {
                    new Vector2(10,10),
                    new Vector2(20,10),
                    new Vector2(20,10),
                    new Vector2(30,10),
                }));
        private ProcessorWatchingImage img;
        private readonly FontCollection FontCollection;
        private readonly Font Font;

        public DrawText()
        {
            this.FontCollection = new FontCollection();
            this.Font = FontCollection.Install(TestFontUtilities.GetPath("SixLaborsSampleAB.woff"));
            this.img = new ProcessorWatchingImage(10, 10);
        }

        public void Dispose()
        {
            img.Dispose();
        }

        [Fact]
        public void FillsForEachACharachterWhenBrushSetAndNotPen()
        {
            img.DrawText("123", this.Font, Brushes.Solid(Color.Red), null, Vector2.Zero, new TextGraphicsOptions(true));

            Assert.NotEmpty(img.ProcessorApplications);
            Assert.Equal(3, img.ProcessorApplications.Count); // 3 fills where applied
            Assert.IsType<FillRegionProcessor<Color>>(img.ProcessorApplications[0].processor);
        }

        [Fact]
        public void FillsForEachACharachterWhenBrushSetAndNotPenDefaultOptions()
        {
            img.DrawText("123", this.Font, Brushes.Solid(Color.Red), null, Vector2.Zero);

            Assert.NotEmpty(img.ProcessorApplications);
            Assert.Equal(3, img.ProcessorApplications.Count); // 3 fills where applied
            Assert.IsType<FillRegionProcessor<Color>>(img.ProcessorApplications[0].processor);
        }


        [Fact]
        public void FillsForEachACharachterWhenBrushSet()
        {
            img.DrawText("123", this.Font, Brushes.Solid(Color.Red), Vector2.Zero, new TextGraphicsOptions(true));

            Assert.NotEmpty(img.ProcessorApplications);
            Assert.Equal(3, img.ProcessorApplications.Count); // 3 fills where applied
            Assert.IsType<FillRegionProcessor<Color>>(img.ProcessorApplications[0].processor);
        }

        [Fact]
        public void FillsForEachACharachterWhenBrushSetDefaultOptions()
        {
            img.DrawText("123", this.Font, Brushes.Solid(Color.Red), Vector2.Zero);

            Assert.NotEmpty(img.ProcessorApplications);
            Assert.Equal(3, img.ProcessorApplications.Count); // 3 fills where applied
            Assert.IsType<FillRegionProcessor<Color>>(img.ProcessorApplications[0].processor);
        }

        [Fact]
        public void FillsForEachACharachterWhenColorSet()
        {
            img.DrawText("123", this.Font, Color.Red, Vector2.Zero, new TextGraphicsOptions(true));

            Assert.NotEmpty(img.ProcessorApplications);
            Assert.Equal(3, img.ProcessorApplications.Count); 
            FillRegionProcessor<Color> processor = Assert.IsType<FillRegionProcessor<Color>>(img.ProcessorApplications[0].processor);

            SolidBrush<Color> brush = Assert.IsType<SolidBrush<Color>>(processor.Brush);
            Assert.Equal(Color.Red, brush.Color);
        }

        [Fact]
        public void FillsForEachACharachterWhenColorSetDefaultOptions()
        {
            img.DrawText("123", this.Font, Color.Red, Vector2.Zero);

            Assert.NotEmpty(img.ProcessorApplications);
            Assert.Equal(3, img.ProcessorApplications.Count); 
            Assert.IsType<FillRegionProcessor<Color>>(img.ProcessorApplications[0].processor);
            FillRegionProcessor<Color> processor = Assert.IsType<FillRegionProcessor<Color>>(img.ProcessorApplications[0].processor);

            SolidBrush<Color> brush = Assert.IsType<SolidBrush<Color>>(processor.Brush);
            Assert.Equal(Color.Red, brush.Color);
        }

        [Fact]
        public void DrawForEachACharachterWhenPenSetAndNotBrush()
        {
            img.DrawText("123", this.Font, null, Pens.Dash(Color.Red, 1), Vector2.Zero, new TextGraphicsOptions(true));

            Assert.NotEmpty(img.ProcessorApplications);
            Assert.Equal(3, img.ProcessorApplications.Count); // 3 fills where applied
            Assert.IsType<DrawPathProcessor<Color>>(img.ProcessorApplications[0].processor);
        }

        [Fact]
        public void DrawForEachACharachterWhenPenSetAndNotBrushDefaultOptions()
        {
            img.DrawText("123", this.Font, null, Pens.Dash(Color.Red, 1), Vector2.Zero);

            Assert.NotEmpty(img.ProcessorApplications);
            Assert.Equal(3, img.ProcessorApplications.Count); // 3 fills where applied
            Assert.IsType<DrawPathProcessor<Color>>(img.ProcessorApplications[0].processor);
        }


        [Fact]
        public void DrawForEachACharachterWhenPenSet()
        {
            img.DrawText("123", this.Font, Pens.Dash(Color.Red, 1), Vector2.Zero, new TextGraphicsOptions(true));

            Assert.NotEmpty(img.ProcessorApplications);
            Assert.Equal(3, img.ProcessorApplications.Count); // 3 fills where applied
            Assert.IsType<DrawPathProcessor<Color>>(img.ProcessorApplications[0].processor);
        }

        [Fact]
        public void DrawForEachACharachterWhenPenSetDefaultOptions()
        {
            img.DrawText("123", this.Font, Pens.Dash(Color.Red, 1), Vector2.Zero);

            Assert.NotEmpty(img.ProcessorApplications);
            Assert.Equal(3, img.ProcessorApplications.Count); // 3 fills where applied
            Assert.IsType<DrawPathProcessor<Color>>(img.ProcessorApplications[0].processor);
        }

        [Fact]
        public void DrawForEachACharachterWhenPenSetAndFillFroEachWhenBrushSet()
        {
            img.DrawText("123", this.Font, Brushes.Solid(Color.Red), Pens.Dash(Color.Red, 1), Vector2.Zero, new TextGraphicsOptions(true));

            Assert.NotEmpty(img.ProcessorApplications);
            Assert.Equal(6, img.ProcessorApplications.Count);
        }

        [Fact]
        public void DrawForEachACharachterWhenPenSetAndFillFroEachWhenBrushSetDefaultOptions()
        {
            img.DrawText("123", this.Font, Brushes.Solid(Color.Red), Pens.Dash(Color.Red, 1), Vector2.Zero);

            Assert.NotEmpty(img.ProcessorApplications);
            Assert.Equal(6, img.ProcessorApplications.Count);
        }

        [Fact]
        public void BrushAppliesBeforPen()
        {
            img.DrawText("1", this.Font, Brushes.Solid(Color.Red), Pens.Dash(Color.Red, 1), Vector2.Zero, new TextGraphicsOptions(true));

            Assert.NotEmpty(img.ProcessorApplications);
            Assert.Equal(2, img.ProcessorApplications.Count);
            Assert.IsType<FillRegionProcessor<Color>>(img.ProcessorApplications[0].processor);
            Assert.IsType<DrawPathProcessor<Color>>(img.ProcessorApplications[1].processor);
        }

        [Fact]
        public void BrushAppliesBeforPenDefaultOptions()
        {
            img.DrawText("1", this.Font, Brushes.Solid(Color.Red), Pens.Dash(Color.Red, 1), Vector2.Zero);

            Assert.NotEmpty(img.ProcessorApplications);
            Assert.Equal(2, img.ProcessorApplications.Count);
            Assert.IsType<FillRegionProcessor<Color>>(img.ProcessorApplications[0].processor);
            Assert.IsType<DrawPathProcessor<Color>>(img.ProcessorApplications[1].processor);
        }
    }
}
