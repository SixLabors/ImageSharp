// <copyright file="ColorConversionTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Drawing
{
    using Drawing;
    using ImageSharp.Drawing;
    using CorePath = ImageSharp.Drawing.Paths.Path;
    using ImageSharp.Drawing.Paths;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.IO;
    using System.Numerics;
    using Xunit;
    using ImageSharp.Drawing.Pens;

    public class DrawStringTests : FileTestBase
    {
        [Fact]
        public void ImageShouldBeOverlayedByText()
        {
            string path = CreateOutputDirectory("Drawing", "String");
            var image = new Image(400, 80);

            var fontFile = new TestFont(TestFonts.Ttf.OpenSans_Regular);
            var font = fontFile.CreateFont();
            font.Size = 50;
            using (FileStream output = File.OpenWrite($"{path}/Simple.png"))
            {
                image
                    .BackgroundColor(Color.Blue)
                    .DrawString("Hello World", font, Color.HotPink, new Vector2(10, 10))
                    .Save(output);
            }
        }
        [Fact]
        public void ImageShouldBeOverlayedByTextNoAntialiasing()
        {
            string path = CreateOutputDirectory("Drawing", "String");
            var image = new Image(400, 80);

            var fontFile = new TestFont(TestFonts.Ttf.OpenSans_Regular);
            var font = fontFile.CreateFont();
            font.Size = 50;
            using (FileStream output = File.OpenWrite($"{path}/NoAntialiasing.png"))
            {
                image
                    .BackgroundColor(Color.Blue)
                    .DrawString("Hello World", font, Color.HotPink, new GraphicsOptions(false), new Vector2(10, 10))
                    .Save(output);
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByTextOutline()
        {
            string path = CreateOutputDirectory("Drawing", "String");
            var image = new Image(400, 80);

            var fontFile = new TestFont(TestFonts.Ttf.OpenSans_Regular);
            var font = fontFile.CreateFont();
            font.Size = 50;
            using (FileStream output = File.OpenWrite($"{path}/Outline.png"))
            {
                image
                    .BackgroundColor(Color.Blue)
                    .DrawString("Hello World", font, Pens.Solid(Color.HotPink, 1), new Vector2(10, 10))
                    .Save(output);
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByTextNewLine()
        {
            string path = CreateOutputDirectory("Drawing", "String");
            var image = new Image(400, 150);

            var fontFile = new TestFont(TestFonts.Ttf.OpenSans_Regular);
            var font = fontFile.CreateFont();
            font.Size = 50;
            using (FileStream output = File.OpenWrite($"{path}/Newline.png"))
            {
                image
                    .BackgroundColor(Color.Blue)
                    .DrawString("Hello\nWorld", font, Color.HotPink, new Vector2(10, 10))
                    .Save(output);
            }
        }

        [Fact]
        public void ImageShouldBeOverlayedByTextNewLineAndWithTab()
        {
            string path = CreateOutputDirectory("Drawing", "String");
            var image = new Image(430, 180);

            var fontFile = new TestFont(TestFonts.Ttf.OpenSans_Regular);
            var font = fontFile.CreateFont();
            font.Size = 50;
            using (FileStream output = File.OpenWrite($"{path}/WithTab.png"))
            {
                image
                    .BackgroundColor(Color.Blue)
                    .DrawString("Helli\tWorld\nHello\tWorld", font, Color.HotPink, new Vector2(10, 10))
                    .Save(output);
            }
        }
    }
}