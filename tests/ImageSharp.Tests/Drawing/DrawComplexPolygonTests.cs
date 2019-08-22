// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Shapes;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing
{
    [GroupOutput("Drawing")]
    public class DrawComplexPolygonTests
    {
        [Theory]
        [WithBasicTestPatternImages(250, 350, PixelTypes.Rgba32, false, false, false)]
        [WithBasicTestPatternImages(250, 350, PixelTypes.Rgba32, true, false, false)]
        [WithBasicTestPatternImages(250, 350, PixelTypes.Rgba32, false, true, false)]
        [WithBasicTestPatternImages(250, 350, PixelTypes.Rgba32, false, false, true)]
        public void DrawComplexPolygon<TPixel>(TestImageProvider<TPixel> provider, bool overlap, bool transparent, bool dashed)
            where TPixel :struct, IPixel<TPixel>
        {
            var simplePath = new Polygon(new LinearLineSegment(
                new Vector2(10, 10),
                new Vector2(200, 150),
                new Vector2(50, 300)));

            var hole1 = new Polygon(new LinearLineSegment(
                new Vector2(37, 85),
                overlap ? new Vector2(130, 40) : new Vector2(93, 85),
                new Vector2(65, 137)));
            IPath clipped = simplePath.Clip(hole1);

            Rgba32 colorRgba = Rgba32.White;
            if (transparent)
            {
                colorRgba.A = 150;
            }

            Color color = colorRgba;

            string testDetails = "";
            if (overlap)
            {
                testDetails += "_Overlap";
            }

            if (transparent)
            {
                testDetails += "_Transparent";
            }

            if (dashed)
            {
                testDetails += "_Dashed";
            }

            Pen pen = dashed ? Pens.Dash(color, 5f) : Pens.Solid(color, 5f);

            provider.RunValidatingProcessorTest(
                x => x.Draw(pen, clipped),
                testDetails,
                appendPixelTypeToFileName: false,
                appendSourceFileOrDescription: false);
        }
    }
}
