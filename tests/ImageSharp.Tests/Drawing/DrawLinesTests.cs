// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing
{
    [GroupOutput("Drawing")]
    public class DrawLinesTests
    {
        [Theory]
        [WithBasicTestPatternImages(250, 350, PixelTypes.Rgba32, "White", 1f, 2.5, true)]
        [WithBasicTestPatternImages(250, 350, PixelTypes.Rgba32, "White", 0.6f, 10, true)]
        [WithBasicTestPatternImages(250, 350, PixelTypes.Rgba32, "White", 1f, 5, false)]
        [WithBasicTestPatternImages(250, 350, PixelTypes.Bgr24, "Yellow", 1f, 10, true)]
        public void DrawLines_Simple<TPixel>(TestImageProvider<TPixel> provider, string colorName, float alpha, float thickness, bool antialias)
            where TPixel : struct, IPixel<TPixel>
        {
            Color color = TestUtils.GetColorByName(colorName).WithAlpha(alpha);
            Pen pen = new Pen(color, thickness);
            
            DrawLinesImpl(provider, colorName, alpha, thickness, antialias, pen);
        }
        
        [Theory]
        [WithBasicTestPatternImages(250, 350, PixelTypes.Rgba32, "White", 1f, 5, false)]
        public void DrawLines_Dash<TPixel>(TestImageProvider<TPixel> provider, string colorName, float alpha, float thickness, bool antialias)
            where TPixel : struct, IPixel<TPixel>
        {
            Color color = TestUtils.GetColorByName(colorName).WithAlpha(alpha);
            Pen pen = Pens.Dash(color, thickness);
            
            DrawLinesImpl(provider, colorName, alpha, thickness, antialias, pen);
        }
        
        [Theory]
        [WithBasicTestPatternImages(250, 350, PixelTypes.Rgba32, "LightGreen", 1f, 5, false)]
        public void DrawLines_Dot<TPixel>(TestImageProvider<TPixel> provider, string colorName, float alpha, float thickness, bool antialias)
            where TPixel : struct, IPixel<TPixel>
        {
            Color color = TestUtils.GetColorByName(colorName).WithAlpha(alpha);
            Pen pen = Pens.Dot(color, thickness);
            
            DrawLinesImpl(provider, colorName, alpha, thickness, antialias, pen);
        }
        
        [Theory]
        [WithBasicTestPatternImages(250, 350, PixelTypes.Rgba32, "Yellow", 1f, 5, false)]
        public void DrawLines_DashDot<TPixel>(TestImageProvider<TPixel> provider, string colorName, float alpha, float thickness, bool antialias)
            where TPixel : struct, IPixel<TPixel>
        {
            Color color = TestUtils.GetColorByName(colorName).WithAlpha(alpha);
            Pen pen = Pens.DashDot(color, thickness);
            
            DrawLinesImpl(provider, colorName, alpha, thickness, antialias, pen);
        }

        [Theory]
        [WithBasicTestPatternImages(250, 350, PixelTypes.Rgba32, "Black", 1f, 5, false)]
        public void DrawLines_DashDotDot<TPixel>(TestImageProvider<TPixel> provider, string colorName, float alpha, float thickness, bool antialias)
            where TPixel : struct, IPixel<TPixel>
        {
            Color color = TestUtils.GetColorByName(colorName).WithAlpha(alpha);
            Pen pen = Pens.DashDotDot(color, thickness);
            
            DrawLinesImpl(provider, colorName, alpha, thickness, antialias, pen);
        }

        
        private static void DrawLinesImpl<TPixel>(
            TestImageProvider<TPixel> provider,
            string colorName,
            float alpha,
            float thickness,
            bool antialias,
            Pen pen)
            where TPixel : struct, IPixel<TPixel>
        {
            SixLabors.Primitives.PointF[] simplePath = { new Vector2(10, 10), new Vector2(200, 150), new Vector2(50, 300) };

            GraphicsOptions options = new GraphicsOptions(antialias);

            string aa = antialias ? "" : "_NoAntialias";
            FormattableString outputDetails = $"{colorName}_A({alpha})_T({thickness}){aa}";

            provider.RunValidatingProcessorTest(
                c => c.DrawLines(options, pen, simplePath),
                outputDetails,
                appendSourceFileOrDescription: false);
        }

    }
}
