// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

using SixLabors.Fonts;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Drawing;
using SixLabors.ImageSharp.Processing.Text;

using Xunit;
// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Drawing.Text
{
    using System;
    using System.Linq;
    using System.Text;
    using SixLabors.ImageSharp.Processing.Drawing.Brushes;
    using SixLabors.ImageSharp.Processing.Drawing.Brushes.GradientBrushes;
    using SixLabors.ImageSharp.Processing.Drawing.Pens;
    using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
    using SixLabors.Primitives;
    using SixLabors.Shapes;

    [GroupOutput("Drawing/Text")]
    public class DrawTextOnImageTests
    {
        private const string AB = "AB\nAB";

        private const string TestText = "Sphinx of black quartz, judge my vow\n0123456789";

        private const string TestText2 =
            "THISISTESTWORDS ";

        [Theory]
        [WithSolidFilledImages(200, 100, "White", PixelTypes.Rgba32, 50, 0, 0, "SixLaborsSampleAB.woff", AB)]
        [WithSolidFilledImages(900, 100, "White", PixelTypes.Rgba32, 50, 0, 0, "OpenSans-Regular.ttf", TestText)]
        [WithSolidFilledImages(400, 40, "White", PixelTypes.Rgba32, 20, 0, 0, "OpenSans-Regular.ttf", TestText)]
        [WithSolidFilledImages(1100, 200, "White", PixelTypes.Rgba32, 50, 150, 100, "OpenSans-Regular.ttf", TestText)]
        public void FontShapesAreRenderedCorrectly<TPixel>(
            TestImageProvider<TPixel> provider,
            int fontSize,
            int x,
            int y,
            string fontName,
            string text)
            where TPixel : struct, IPixel<TPixel>
        {
            Font font = CreateFont(fontName, fontSize);
            string fnDisplayText = text.Replace("\n", "");
            fnDisplayText = fnDisplayText.Substring(0, Math.Min(fnDisplayText.Length, 4));
            TPixel color = NamedColors<TPixel>.Black;

            provider.VerifyOperation(
                ImageComparer.Tolerant(imageThreshold: 0.1f, perPixelManhattanThreshold: 20),
                img =>
                {
                    img.Mutate(c => c.DrawText(text, new Font(font, fontSize), color, new PointF(x, y)));
                },
                $"{fontName}-{fontSize}-{fnDisplayText}-({x},{y})",
                appendPixelTypeToFileName: false,
                appendSourceFileOrDescription: true);
        }

        /// <summary>
        /// Based on:
        /// https://github.com/SixLabors/ImageSharp/issues/572
        /// </summary>
        [Theory]
        [WithSolidFilledImages(2480, 3508, "White", PixelTypes.Rgba32)]
        public void FontShapesAreRenderedCorrectly_LargeText<TPixel>(
            TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            Font font = CreateFont("OpenSans-Regular.ttf", 36);

            var sb = new StringBuilder();
            string str = Repeat(" ", 78) + "THISISTESTWORDSTHISISTESTWORDSTHISISTESTWORDSTHISISTESTWORDSTHISISTESTWORDS";
            sb.Append(str);

            string newLines = Repeat(Environment.NewLine, 80);
            sb.Append(newLines);

            for (int i = 0; i < 10; i++)
            {
                sb.AppendLine(str);
            }

            var textOptions = new TextGraphicsOptions
            {
                Antialias = true,
                ApplyKerning = true,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            TPixel color = NamedColors<TPixel>.Black;

            provider.VerifyOperation(
                ImageComparer.Tolerant(imageThreshold: 0.1f, perPixelManhattanThreshold: 20),
                img =>
                    {
                        img.Mutate(c => c.DrawText(textOptions, sb.ToString(), font, color, new PointF(10, 5)));
                    },
                false,
                false);
        }

        [Theory]
        [WithSolidFilledImages(200, 100, "White", PixelTypes.Rgba32, 50, 0, 0, "SixLaborsSampleAB.woff", AB)]
        [WithSolidFilledImages(900, 100, "White", PixelTypes.Rgba32, 50, 0, 0, "OpenSans-Regular.ttf", TestText)]
        [WithSolidFilledImages(1100, 200, "White", PixelTypes.Rgba32, 50, 150, 100, "OpenSans-Regular.ttf", TestText)]
        public void FontShapesAreRenderedCorrectlyWithAPen<TPixel>(
            TestImageProvider<TPixel> provider,
            int fontSize,
            int x,
            int y,
            string fontName,
            string text)
            where TPixel : struct, IPixel<TPixel>
        {
            Font font = CreateFont(fontName, fontSize);
            string fnDisplayText = text.Replace("\n", "");
            fnDisplayText = fnDisplayText.Substring(0, Math.Min(fnDisplayText.Length, 4));
            TPixel color = NamedColors<TPixel>.Black;

            provider.VerifyOperation(
                ImageComparer.Tolerant(imageThreshold: 0.1f, perPixelManhattanThreshold: 20),
                img =>
                {
                    img.Mutate(c => c.DrawText(text, new Font(font, fontSize), null, Pens.Solid(color, 1), new PointF(x, y)));
                },
                $"pen_{fontName}-{fontSize}-{fnDisplayText}-({x},{y})",
                appendPixelTypeToFileName: false,
                appendSourceFileOrDescription: true);
        }

        [Theory]
        [WithSolidFilledImages(200, 100, "White", PixelTypes.Rgba32, 50, 0, 0, "SixLaborsSampleAB.woff", AB)]
        [WithSolidFilledImages(900, 100, "White", PixelTypes.Rgba32, 50, 0, 0, "OpenSans-Regular.ttf", TestText)]
        [WithSolidFilledImages(1100, 200, "White", PixelTypes.Rgba32, 50, 150, 100, "OpenSans-Regular.ttf", TestText)]
        public void FontShapesAreRenderedCorrectlyWithAPenPatterned<TPixel>(
            TestImageProvider<TPixel> provider,
            int fontSize,
            int x,
            int y,
            string fontName,
            string text)
            where TPixel : struct, IPixel<TPixel>
        {
            Font font = CreateFont(fontName, fontSize);
            TPixel color = NamedColors<TPixel>.Black;

            provider.VerifyOperation(
                ImageComparer.Tolerant(imageThreshold: 0.1f, perPixelManhattanThreshold: 20),
                img =>
                {
                    img.Mutate(c => c.DrawText(text, new Font(font, fontSize), null, Pens.DashDot(color, 3), new PointF(x, y)));
                },
                $"pen_{fontName}-{fontSize}-{ToTestOutputDisplayText(text)}-({x},{y})",
                appendPixelTypeToFileName: false,
                appendSourceFileOrDescription: true);
        }

        [Theory]
        [WithSolidFilledImages(200, 100, "White", PixelTypes.Rgba32, 50, "SixLaborsSampleAB.woff", AB)]
        [WithSolidFilledImages(900, 100, "White", PixelTypes.Rgba32, 50, "OpenSans-Regular.ttf", TestText)]
        public void FontShapesAreRenderedCorrectlyAlongAPath<TPixel>(
            TestImageProvider<TPixel> provider,
            int fontSize,
            string fontName,
            string text)
            where TPixel : struct, IPixel<TPixel>
        {
            Font font = CreateFont(fontName, fontSize);
            TPixel colorFill = NamedColors<TPixel>.Gray;
            TPixel colorOutline = NamedColors<TPixel>.Black;
            IBrush<TPixel> fillBrush = Brushes.Solid(colorFill);
            IPen<TPixel> outlinePen = Pens.DashDot(colorOutline, 3);

            provider.VerifyOperation(
                ImageComparer.Tolerant(imageThreshold: 0.1f, perPixelManhattanThreshold: 20),
                img =>
                    {
                        IPath path = new Path(new LinearLineSegment(new Point(0, img.Height), new Point(img.Width, 0)));
                        img.Mutate(
                            c =>
                                {
                                    c.DrawText(
                                        new TextGraphicsOptions
                                            {
                                                HorizontalAlignment = HorizontalAlignment.Center,
                                                VerticalAlignment = VerticalAlignment.Top
                                            },
                                        text,
                                        new Font(font, fontSize),
                                        fillBrush,
                                        outlinePen,
                                        path);
                                });
                    },
                $"pen_{fontName}-{fontSize}-{ToTestOutputDisplayText(text)}",
                appendPixelTypeToFileName: false,
                appendSourceFileOrDescription: true);
        }

        [Theory]
        [WithSolidFilledImages(600, 600, "White", PixelTypes.Rgba32, 50, "OpenSans-Regular.ttf", TestText)]
        public void FontShapesAreRenderedCorrectlyAlongACirclePath<TPixel>(
            TestImageProvider<TPixel> provider,
            int fontSize,
            string fontName,
            string text)
            where TPixel : struct, IPixel<TPixel>
        {

            Font font = CreateFont(fontName, fontSize);
            TPixel colorFill = NamedColors<TPixel>.Black;
            IBrush<TPixel> fillBrush = Brushes.Solid(colorFill);
            
            provider.VerifyOperation(
                ImageComparer.Tolerant(imageThreshold: 0.1f, perPixelManhattanThreshold: 20),
                img =>
                {
                    int w = (int)(img.Width * 0.6);
                    int h = (int)(img.Height * 0.6);
                    IPath path = new EllipsePolygon(img.Width/2, img.Height/2, w, h);

                    img.Mutate(c =>
                    {
                        c.DrawText(
                                new TextGraphicsOptions
                                {
                                    HorizontalAlignment = HorizontalAlignment.Center,
                                    VerticalAlignment = VerticalAlignment.Top
                                },
                                text,
                                new Font(font, fontSize),
                                fillBrush,
                                path);
                    });
                },
                $"pen_{fontName}-{fontSize}-{ToTestOutputDisplayText(text)}",
                appendPixelTypeToFileName: false,
                appendSourceFileOrDescription: true);
        }

        private static string Repeat(string str, int times) => string.Concat(Enumerable.Repeat(str, times));

        private static string ToTestOutputDisplayText(string text)
        {
            string fnDisplayText = text.Replace("\n", "");
            fnDisplayText = fnDisplayText.Substring(0, Math.Min(fnDisplayText.Length, 4));
            return fnDisplayText;
        }

        private static Font CreateFont(string fontName, int size)
        {
            var fontCollection = new FontCollection();
            string fontPath = TestFontUtilities.GetPath(fontName);
            Font font = fontCollection.Install(fontPath).CreateFont(size);
            return font;
        }
    }
}
