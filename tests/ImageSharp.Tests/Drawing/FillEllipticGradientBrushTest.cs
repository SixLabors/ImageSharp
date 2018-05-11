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
    [GroupOutput("Drawing/GradientBrushes")]
    public class FillEllipticGradientBrushTests : FileTestBase
    {
        [Theory]
        [WithBlankImages(10, 10, PixelTypes.Rgba32)]
        public void WithEqualColorsReturnsUnicolorImage<TPixel>(
            TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel red = NamedColors<TPixel>.Red;

            using (Image<TPixel> image = provider.GetImage())
            {
                EllipticGradientBrush<TPixel> unicolorLinearGradientBrush =
                    new EllipticGradientBrush<TPixel>(
                        new SixLabors.Primitives.Point(0, 0),
                        new SixLabors.Primitives.Point(10, 0),
                        1.0f,
                        GradientRepetitionMode.None,
                        new ColorStop<TPixel>(0, red),
                        new ColorStop<TPixel>(1, red));

                image.Mutate(x => x.Fill(unicolorLinearGradientBrush));
                image.DebugSave(provider);

                using (PixelAccessor<TPixel> sourcePixels = image.Lock())
                {
                    Assert.Equal(red, sourcePixels[0, 0]);
                    Assert.Equal(red, sourcePixels[9, 9]);
                    Assert.Equal(red, sourcePixels[5, 5]);
                    Assert.Equal(red, sourcePixels[3, 8]);
                }

                image.CompareToReferenceOutput(provider);
            }
        }

        [Theory]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, 0.1)]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, 0.4)]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, 0.8)]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, 1.0)]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, 1.2)]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, 1.6)]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, 2.0)]
        public void AxisParallelEllipsesWithDifferentRatio<TPixel>(
            TestImageProvider<TPixel> provider,
            float ratio)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel yellow = NamedColors<TPixel>.Yellow;
            TPixel red = NamedColors<TPixel>.Red;
            TPixel black = NamedColors<TPixel>.Black;

            using (var image = provider.GetImage())
            {
                EllipticGradientBrush<TPixel> unicolorLinearGradientBrush =
                    new EllipticGradientBrush<TPixel>(
                        new SixLabors.Primitives.Point(image.Width / 2, image.Height / 2),
                        new SixLabors.Primitives.Point(image.Width / 2, (image.Width * 2) / 3),
                        ratio,
                        GradientRepetitionMode.None,
                        new ColorStop<TPixel>(0, yellow),
                        new ColorStop<TPixel>(1, red),
                        new ColorStop<TPixel>(1, black));

                image.Mutate(x => x.Fill(unicolorLinearGradientBrush));
                image.DebugSave(provider, ratio.ToString("F1"));
                image.CompareToReferenceOutput(provider, ratio);
            }
        }

        [Theory]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, 0.1, 0)]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, 0.4, 0)]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, 0.8, 0)]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, 1.0, 0)]

        [WithBlankImages(200, 200, PixelTypes.Rgba32, 0.1, 45)]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, 0.4, 45)]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, 0.8, 45)]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, 1.0, 45)]

        [WithBlankImages(200, 200, PixelTypes.Rgba32, 0.1, 90)]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, 0.4, 90)]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, 0.8, 90)]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, 1.0, 90)]

        [WithBlankImages(200, 200, PixelTypes.Rgba32, 0.1, 30)]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, 0.4, 30)]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, 0.8, 30)]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, 1.0, 30)]
        public void RotatedEllipsesWithDifferentRatio<TPixel>(
            TestImageProvider<TPixel> provider,
            float ratio,
            float rotationInDegree)
            where TPixel: struct, IPixel<TPixel>
        {
            string variant = $"{ratio:F2}at{rotationInDegree:00}°";

            using (var image = provider.GetImage())
            {
                TPixel yellow = NamedColors<TPixel>.Yellow;
                TPixel red = NamedColors<TPixel>.Red;
                TPixel black = NamedColors<TPixel>.Black;

                var center = new SixLabors.Primitives.Point(image.Width / 2, image.Height / 2);

                var rotation = (Math.PI * rotationInDegree) / 180.0;
                var cos = Math.Cos(rotation);
                var sin = Math.Sin(rotation);

                int offsetY = image.Height / 6;
                int axisX = center.X + (int)-(offsetY * sin);
                int axisY = center.Y + (int)(offsetY * cos);

                EllipticGradientBrush<TPixel> unicolorLinearGradientBrush =
                    new EllipticGradientBrush<TPixel>(
                        center,
                        new SixLabors.Primitives.Point(axisX, axisY),
                        ratio,
                        GradientRepetitionMode.None,
                        new ColorStop<TPixel>(0, yellow),
                        new ColorStop<TPixel>(1, red),
                        new ColorStop<TPixel>(1, black));

                image.Mutate(x => x.Fill(unicolorLinearGradientBrush));
                image.DebugSave(provider, variant);
                image.CompareToReferenceOutput(provider, variant);
            }
        }
    }
}