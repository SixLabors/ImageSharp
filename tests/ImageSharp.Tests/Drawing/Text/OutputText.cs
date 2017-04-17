
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

    public class OutputText : FileTestBase
    {
        private readonly FontCollection FontCollection;
        private readonly Font Font;

        public OutputText()
        {
            this.FontCollection = new FontCollection();
            this.Font = FontCollection.Install(TestFontUtilities.GetPath("SixLaborsSampleAB.woff"));
        }

        [Fact]
        public void DrawAB()
        {
            //draws 2 overlapping triangle glyphs twice 1 set on each line
            using (Image img = new Image(100, 200))
            {
                img.Fill(Color.DarkBlue)
                   .DrawText("AB\nAB", new Font(this.Font, 50), Color.Red, new Vector2(0, 0));
                img.Save($"{this.CreateOutputDirectory("Drawing", "Text")}/AB.png");
            }
        }
    }
}
