// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing.Drawing;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing
{
    using System;

    using SixLabors.ImageSharp.Processing;
    using SixLabors.ImageSharp.Processing.Drawing.Brushes;
    using SixLabors.ImageSharp.Processing.Overlays;

    using Point = SixLabors.Primitives.Point;

    public class FillLinearGradientBrushTests : FileTestBase
    {
        [Fact]
        public void LinearGradientBrushWithEqualColorsReturnsUnicolorImage()
        {
            string path = TestEnvironment.CreateOutputDirectory("Fill", "LinearGradientBrush");
            using (var image = new Image<Rgba32>(500, 500))
            {
                LinearGradientBrush<Rgba32> unicolorLinearGradientBrush = 
                    new LinearGradientBrush<Rgba32>(
                        new Point(0, 0),
                        new Point(500, 0),
                        new Tuple<float, Rgba32>(0, Rgba32.Red),
                        new Tuple<float, Rgba32>(1, Rgba32.Red));
                
                image.Mutate(x => x.Fill(unicolorLinearGradientBrush));
                image.Save($"{path}/UnicolorGradient.png");

                using (PixelAccessor<Rgba32> sourcePixels = image.Lock())
                {
                    Assert.Equal(Rgba32.Red, sourcePixels[0, 0]);
                    Assert.Equal(Rgba32.Red, sourcePixels[9, 9]);
                    Assert.Equal(Rgba32.Red, sourcePixels[199, 149]);
                    Assert.Equal(Rgba32.Red, sourcePixels[500, 500]);
                }
            }
        }
    }
}
