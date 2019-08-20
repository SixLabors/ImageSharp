// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Shapes;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing
{
    [GroupOutput("Drawing")]
    public class FillPolygonTests
    {
        [Theory]
        [WithBasicTestPatternImages(250, 350, PixelTypes.Rgba32, "White", 1f, true)]
        [WithBasicTestPatternImages(250, 350, PixelTypes.Rgba32, "White", 0.6f, true)]
        [WithBasicTestPatternImages(250, 350, PixelTypes.Rgba32, "White", 1f, false)]
        [WithBasicTestPatternImages(250, 350, PixelTypes.Bgr24, "Yellow", 1f, true)]
        public void FillPolygon_Solid<TPixel>(TestImageProvider<TPixel> provider, string colorName, float alpha, bool antialias)
            where TPixel : struct, IPixel<TPixel>
        {
            SixLabors.Primitives.PointF[] simplePath =
                {
                    new Vector2(10, 10), new Vector2(200, 150), new Vector2(50, 300)
                };
            Color color = TestUtils.GetColorByName(colorName).WithAlpha(alpha);

            var options = new GraphicsOptions(antialias);

            string aa = antialias ? "" : "_NoAntialias";
            FormattableString outputDetails = $"{colorName}_A{alpha}{aa}";

            provider.RunValidatingProcessorTest(
                c => c.FillPolygon(options, color, simplePath),
                outputDetails,
                appendSourceFileOrDescription: false);
        }

        [Theory]
        [WithBasicTestPatternImages(200, 200, PixelTypes.Rgba32)]
        public void FillPolygon_Concave<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            var points = new SixLabors.Primitives.PointF[]
                             {
                                 new Vector2(8, 8),
                                 new Vector2(64, 8),
                                 new Vector2(64, 64),
                                 new Vector2(120, 64),
                                 new Vector2(120, 120),
                                 new Vector2(8, 120)
                             };

            var color = Color.LightGreen;

            provider.RunValidatingProcessorTest(
                c => c.FillPolygon(color, points),
                appendSourceFileOrDescription: false,
                appendPixelTypeToFileName: false);
        }

        [Theory]
        [WithBasicTestPatternImages(250, 350, PixelTypes.Rgba32)]
        public void FillPolygon_Pattern<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            SixLabors.Primitives.PointF[] simplePath =
                {
                    new Vector2(10, 10), new Vector2(200, 150), new Vector2(50, 300)
                };
            var color = Color.Yellow;

            var brush = Brushes.Horizontal(color);

            provider.RunValidatingProcessorTest(
                c => c.FillPolygon(brush, simplePath),
                appendSourceFileOrDescription: false);
        }

        [Theory]
        [WithBasicTestPatternImages(250, 350, PixelTypes.Rgba32, TestImages.Png.Ducky)]
        [WithBasicTestPatternImages(250, 350, PixelTypes.Rgba32, TestImages.Bmp.Car)]
        public void FillPolygon_ImageBrush<TPixel>(TestImageProvider<TPixel> provider, string brushImageName)
            where TPixel : struct, IPixel<TPixel>
        {
            SixLabors.Primitives.PointF[] simplePath =
                {
                    new Vector2(10, 10), new Vector2(200, 50), new Vector2(50, 200)
                };

            using (Image<TPixel> brushImage = Image.Load<TPixel>(TestFile.Create(brushImageName).Bytes))
            {
                var brush = new ImageBrush(brushImage);

                provider.RunValidatingProcessorTest(
                    c => c.FillPolygon(brush, simplePath),
                    System.IO.Path.GetFileNameWithoutExtension(brushImageName),
                    appendSourceFileOrDescription: false);
            }
        }

        [Theory]
        [WithBasicTestPatternImages(250, 250, PixelTypes.Rgba32)]
        public void Fill_RectangularPolygon<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            var polygon = new SixLabors.Shapes.RectangularPolygon(10, 10, 190, 140);
            var color = Color.White;

            provider.RunValidatingProcessorTest(
                c => c.Fill(color, polygon),
                appendSourceFileOrDescription: false);
        }

        [Theory]
        [WithBasicTestPatternImages(200, 200, PixelTypes.Rgba32, 3, 50, 0f)]
        [WithBasicTestPatternImages(200, 200, PixelTypes.Rgba32, 3, 60, 20f)]
        [WithBasicTestPatternImages(200, 200, PixelTypes.Rgba32, 3, 60, -180f)]
        [WithBasicTestPatternImages(200, 200, PixelTypes.Rgba32, 5, 70, 0f)]
        [WithBasicTestPatternImages(200, 200, PixelTypes.Rgba32, 7, 80, -180f)]
        public void Fill_RegularPolygon<TPixel>(TestImageProvider<TPixel> provider, int vertices, float radius, float angleDeg)
            where TPixel : struct, IPixel<TPixel>
        {
            float angle = GeometryUtilities.DegreeToRadian(angleDeg);
            var polygon = new RegularPolygon(100, 100, vertices, radius, angle);
            var color = Color.Yellow;

            FormattableString testOutput = $"V({vertices})_R({radius})_Ang({angleDeg})";
            provider.RunValidatingProcessorTest(
                c => c.Fill(color, polygon),
                testOutput,
                appendSourceFileOrDescription: false,
                appendPixelTypeToFileName: false);
        }

        [Theory]
        [WithBasicTestPatternImages(200, 200, PixelTypes.Rgba32)]
        public void Fill_EllipsePolygon<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            var polygon = new EllipsePolygon(100, 100, 80, 120);
            var color = Color.Azure;

            provider.RunValidatingProcessorTest(
                c => c.Fill(color, polygon),
                appendSourceFileOrDescription: false,
                appendPixelTypeToFileName: false);
        }
    }
}
