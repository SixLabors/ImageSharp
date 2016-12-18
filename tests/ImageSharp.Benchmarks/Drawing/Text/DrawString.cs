// <copyright file="Crop.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Benchmarks
{
    using System.Drawing;
    using System.Drawing.Drawing2D;

    using BenchmarkDotNet.Attributes;
    using CoreImage = ImageSharp.Image;
    using CorePoint = ImageSharp.Point;
    using CoreColor = ImageSharp.Color;
    using CoreFont = ImageSharp.Drawing.Font;
    using System.IO;
    using System.Numerics;
    using Drawing;
    using System.Linq;
    using System.Diagnostics;
    using System.Drawing.Text;

    public class DrawStrings
    {
        private readonly CoreFont coreFont;
        private readonly PrivateFontCollection fontscollection;
        private readonly System.Drawing.Font font;

        public DrawStrings()
        {
            this.fontscollection = new System.Drawing.Text.PrivateFontCollection();
            fontscollection.AddFontFile("TestFonts/Formats/TTF/OpenSans-Regular.ttf");
            var familiy = fontscollection.Families[0];
            this.font = new System.Drawing.Font(familiy, 10);
            using (var fs = File.OpenRead("TestFonts/Formats/TTF/OpenSans-Regular.ttf"))
            {
                this.coreFont = new CoreFont(fs);
            }
        }

        [Benchmark(Baseline = true, Description = "System.Drawing Draw String")]
        public void DrawStringSystemDrawing()
        {

            using (Bitmap destination = new Bitmap(800, 800))
            {
                using (Graphics graphics = Graphics.FromImage(destination))
                {
                    graphics.InterpolationMode = InterpolationMode.Default;
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.DrawString("Hello World", font, Brushes.HotPink, 10, 10);
                }

                using (MemoryStream ms = new MemoryStream())
                {
                    destination.Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                }
            }
        }

        [Benchmark(Description = "ImageSharp Draw String")]
        public void DrawLinesCore()
        {
            CoreImage image = new CoreImage(800, 800);
            image.DrawString("Hello World", coreFont, CoreColor.HotPink, new Vector2(10, 10));

            using (MemoryStream ms = new MemoryStream())
            {
                image.SaveAsBmp(ms);
            }
        }
    }
}