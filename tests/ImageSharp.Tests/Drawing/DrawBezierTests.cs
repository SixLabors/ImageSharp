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
    public class DrawBezierTests
    {
        public static readonly TheoryData<string, byte, float> DrawPathData = new TheoryData<string, byte, float>
                                                                                  {
                                                                                      { "White", 255, 1.5f },
                                                                                      { "Red", 255, 3 },
                                                                                      { "HotPink", 255, 5 },
                                                                                      { "HotPink", 150, 5 },
                                                                                      { "White", 255, 15 },
                                                                                  };

        [Theory]
        [WithSolidFilledImages(nameof(DrawPathData), 300, 450, "Blue", PixelTypes.Rgba32)]
        public void DrawBeziers<TPixel>(TestImageProvider<TPixel> provider, string colorName, byte alpha, float thickness)
            where TPixel : struct, IPixel<TPixel>
        {
            var points = new SixLabors.Primitives.PointF[]
                             {
                                 new Vector2(10, 400), new Vector2(30, 10), new Vector2(240, 30), new Vector2(300, 400)
                             };
            Rgba32 rgba = TestUtils.GetColorByName(colorName);
            rgba.A = alpha;
            Color color = rgba;

            FormattableString testDetails = $"{colorName}_A{alpha}_T{thickness}";
            
            provider.RunValidatingProcessorTest( x => x.DrawBeziers(color, 5f, points), 
                testDetails,
                appendSourceFileOrDescription: false,
                appendPixelTypeToFileName: false);
        }
    }
}
