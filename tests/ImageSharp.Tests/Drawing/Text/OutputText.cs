// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Brushes;
using SixLabors.ImageSharp.Drawing.Pens;
using SixLabors.ImageSharp.Drawing.Processors;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Fonts;
using SixLabors.Shapes;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing.Text
{
    public class OutputText : FileTestBase
    {
        private readonly FontCollection FontCollection;
        private readonly Font Font;

        public OutputText()
        {
            this.FontCollection = new FontCollection();
            this.Font = FontCollection.Install(TestFontUtilities.GetPath("SixLaborsSampleAB.woff")).CreateFont(12);
        }

        [Fact]
        public void DrawAB()
        {
            //draws 2 overlapping triangle glyphs twice 1 set on each line
            using (Image<Rgba32> img = new Image<Rgba32>(100, 200))
            {
                img.Mutate(x => x.Fill(Rgba32.DarkBlue)
                   .DrawText("AB\nAB", new Font(this.Font, 50), Rgba32.Red, new Vector2(0, 0)));
                img.Save($"{TestEnvironment.CreateOutputDirectory("Drawing", "Text")}/AB.png");
            }
        }
    }
}
