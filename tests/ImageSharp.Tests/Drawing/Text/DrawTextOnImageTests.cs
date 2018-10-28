// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Linq;
using System.Text;
using SixLabors.Fonts;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.Primitives;

using Xunit;
using Xunit.Abstractions;

// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Drawing.Text
{
    [GroupOutput("Drawing/Text")]
    public class DrawTextOnImageTests
    {
        private const string AB = "AB\nAB";

        private const string TestText = "Sphinx of black quartz, judge my vow\n0123456789";

        public static ImageComparer TextDrawingComparer = ImageComparer.TolerantPercentage(1e-5f);
        public static ImageComparer OutlinedTextDrawingComparer = ImageComparer.TolerantPercentage(1e-4f);

        public DrawTextOnImageTests(ITestOutputHelper output)
        {
            this.Output = output;
        }

        private ITestOutputHelper Output { get; }

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
            TPixel color = NamedColors<TPixel>.Black;

            provider.VerifyOperation(
                TextDrawingComparer,
                img =>
                {
                    img.Mutate(c => c.DrawText(text, new Font(font, fontSize), color, new PointF(x, y)));
                },
                $"{fontName}-{fontSize}-{ToTestOutputDisplayText(text)}-({x},{y})",
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

            // Based on the reported 0.0270% difference with AccuracyMultiple = 8
            // We should avoid quality regressions leading to higher difference!
            var comparer = ImageComparer.TolerantPercentage(0.03f);

            provider.VerifyOperation(
                comparer,
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
            TPixel color = NamedColors<TPixel>.Black;

            provider.VerifyOperation(
                OutlinedTextDrawingComparer,
                img =>
                {
                    img.Mutate(c => c.DrawText(text, new Font(font, fontSize), null, Pens.Solid(color, 1), new PointF(x, y)));
                },
                $"pen_{fontName}-{fontSize}-{ToTestOutputDisplayText(text)}-({x},{y})",
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
                OutlinedTextDrawingComparer,
                img =>
                {
                    img.Mutate(c => c.DrawText(text, new Font(font, fontSize), null, Pens.DashDot(color, 3), new PointF(x, y)));
                },
                $"pen_{fontName}-{fontSize}-{ToTestOutputDisplayText(text)}-({x},{y})",
                appendPixelTypeToFileName: false,
                appendSourceFileOrDescription: true);
        }

        [Theory]
        [WithSolidFilledImages(1000, 1500, "White", PixelTypes.Rgba32, "Baskerville Old Face")]
        [WithSolidFilledImages(1000, 1500, "White", PixelTypes.Rgba32, "Arial")]
        public void TextPositioningIsRobust<TPixel>(TestImageProvider<TPixel> provider, string fontName)
            where TPixel : struct, IPixel<TPixel>
        {
            if (!TestEnvironment.IsWindows)
            {
                // Does the system font "Baskerville Old Face" exist on most Linux/MAC machines?
                return;
            }
            
            Font font = SystemFonts.CreateFont(fontName, 30, FontStyle.Regular);

            string text = Repeat("Beware the Jabberwock, my son!  The jaws that bite, the claws that catch!  Beware the Jubjub bird, and shun The frumious Bandersnatch!\n",
                20);
            var textOptions = new TextGraphicsOptions(true) { WrapTextWidth = 1000 };

            string details = fontName.Replace(" ", "");

            // Based on the reported 0.1755% difference with AccuracyMultiple = 8
            // We should avoid quality regressions leading to higher difference!
            var comparer = ImageComparer.TolerantPercentage(0.2f);

            provider.RunValidatingProcessorTest(
                x => x.DrawText(textOptions, text, font, NamedColors<TPixel>.Black, new PointF(10, 50)),
                details,
                comparer,
                appendPixelTypeToFileName: false,
                appendSourceFileOrDescription: false);
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
