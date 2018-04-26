// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Drawing;
using SixLabors.ImageSharp.Processing.Drawing.Brushes.GradientBrushes;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing
{
    public class FillEllipticGradientBrushTests : FileTestBase
    {
        [Fact]
        public void EllipticGradientBrushWithEqualColorsAndReturnsUnicolorImage()
        {
            string path = TestEnvironment.CreateOutputDirectory("Fill", "EllipticGradientBrush");
            using (var image = new Image<Rgba32>(10, 10))
            {
                EllipticGradientBrush<Rgba32> unicolorLinearGradientBrush =
                    new EllipticGradientBrush<Rgba32>(
                        new SixLabors.Primitives.Point(0, 0),
                        new SixLabors.Primitives.Point(10, 0),
                        1.0f,
                        GradientRepetitionMode.None,
                        new ColorStop<Rgba32>(0, Rgba32.Red),
                        new ColorStop<Rgba32>(1, Rgba32.Red));

                image.Mutate(x => x.Fill(unicolorLinearGradientBrush));
                image.Save($"{path}/UnicolorCircleGradient.png");

                using (PixelAccessor<Rgba32> sourcePixels = image.Lock())
                {
                    Assert.Equal(Rgba32.Red, sourcePixels[0, 0]);
                    Assert.Equal(Rgba32.Red, sourcePixels[9, 9]);
                    Assert.Equal(Rgba32.Red, sourcePixels[5, 5]);
                    Assert.Equal(Rgba32.Red, sourcePixels[3, 8]);
                }
            }
        }

        [Theory]
        [InlineData(0.1)]
        [InlineData(0.4)]
        [InlineData(0.8)]
        [InlineData(1.0)]
        [InlineData(1.2)]
        [InlineData(1.6)]
        [InlineData(2.0)]
        public void EllipticGradientBrushProducesAxisParallelEllipsesWithDifferentRatio(
            float ratio)
        {
            string path = TestEnvironment.CreateOutputDirectory("Fill", "EllipticGradientBrush");
            using (var image = new Image<Rgba32>(1000, 1000))
            {
                EllipticGradientBrush<Rgba32> unicolorLinearGradientBrush =
                    new EllipticGradientBrush<Rgba32>(
                        new SixLabors.Primitives.Point(500, 500),
                        new SixLabors.Primitives.Point(500, 750),
                        ratio,
                        GradientRepetitionMode.None,
                        new ColorStop<Rgba32>(0, Rgba32.Yellow),
                        new ColorStop<Rgba32>(1, Rgba32.Red),
                        new ColorStop<Rgba32>(1, Rgba32.Black));

                image.Mutate(x => x.Fill(unicolorLinearGradientBrush));
                image.Save($"{path}/Ellipsis{ratio}.png");
            }
        }

        [Theory]
        [InlineData(0.1, 0)]
        [InlineData(0.4, 0)]
        [InlineData(0.8, 0)]
        [InlineData(1.0, 0)]

        [InlineData(0.1, 45)]
        [InlineData(0.4, 45)]
        [InlineData(0.8, 45)]
        [InlineData(1.0, 45)]

        [InlineData(0.1, 90)]
        [InlineData(0.4, 90)]
        [InlineData(0.8, 90)]
        [InlineData(1.0, 90)]

        [InlineData(0.1, 30)]
        [InlineData(0.4, 30)]
        [InlineData(0.8, 30)]
        [InlineData(1.0, 30)]
        public void EllipticGradientBrushProducesRotatedEllipsesWithDifferentRatio(
            float ratio,
            float rotationInDegree)
        {
            var center = new SixLabors.Primitives.Point(500, 500);

            var rotation = (Math.PI * rotationInDegree) / 180.0;
            var cos = Math.Cos(rotation);
            var sin = Math.Sin(rotation);

            int axisX = (int)((center.X * cos) - (center.Y * sin));
            int axisY = (int)((center.X * sin) + (center.Y * cos));

            string path = TestEnvironment.CreateOutputDirectory("Fill", "EllipticGradientBrush");
            using (var image = new Image<Rgba32>(1000, 1000))
            {
                EllipticGradientBrush<Rgba32> unicolorLinearGradientBrush =
                    new EllipticGradientBrush<Rgba32>(
                        center,
                        new SixLabors.Primitives.Point(axisX, axisY),
                        ratio,
                        GradientRepetitionMode.None,
                        new ColorStop<Rgba32>(0, Rgba32.Yellow),
                        new ColorStop<Rgba32>(1, Rgba32.Red),
                        new ColorStop<Rgba32>(1, Rgba32.Black));

                image.Mutate(x => x.Fill(unicolorLinearGradientBrush));
                image.Save($"{path}/Ellipsis{ratio}_rot{rotationInDegree}°.png");
            }
        }
    }
}