// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing
{
    using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

    [GroupOutput("Drawing/GradientBrushes")]
    public class FillEllipticGradientBrushTests
    {
        public static ImageComparer TolerantComparer = ImageComparer.TolerantPercentage(0.01f);

        [Theory]
        [WithBlankImages(10, 10, PixelTypes.Rgba32)]
        public void WithEqualColorsReturnsUnicolorImage<TPixel>(
            TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            TPixel red = NamedColors<TPixel>.Red;

            using (Image<TPixel> image = provider.GetImage())
            {
                var unicolorLinearGradientBrush =
                    new EllipticGradientBrush<TPixel>(
                        new SixLabors.Primitives.Point(0, 0),
                        new SixLabors.Primitives.Point(10, 0),
                        1.0f,
                        GradientRepetitionMode.None,
                        new ColorStop<TPixel>(0, red),
                        new ColorStop<TPixel>(1, red));

                image.Mutate(x => x.Fill(unicolorLinearGradientBrush));

                image.DebugSave(provider, appendPixelTypeToFileName: false, appendSourceFileOrDescription: false);

                // no need for reference image in this test:
                image.ComparePixelBufferTo(red);
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

            provider.VerifyOperation(
                TolerantComparer,
                image =>
                    {
                        var unicolorLinearGradientBrush = new EllipticGradientBrush<TPixel>(
                            new SixLabors.Primitives.Point(image.Width / 2, image.Height / 2),
                            new SixLabors.Primitives.Point(image.Width / 2, (image.Width * 2) / 3),
                            ratio,
                            GradientRepetitionMode.None,
                            new ColorStop<TPixel>(0, yellow),
                            new ColorStop<TPixel>(1, red),
                            new ColorStop<TPixel>(1, black));

                        image.Mutate(x => x.Fill(unicolorLinearGradientBrush));
                    },
                $"{ratio:F2}",
                false,
                false);
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
            FormattableString variant = $"{ratio:F2}_AT_{rotationInDegree:00}deg";

            provider.VerifyOperation(
                TolerantComparer,
                image =>
                    {
                        TPixel yellow = NamedColors<TPixel>.Yellow;
                        TPixel red = NamedColors<TPixel>.Red;
                        TPixel black = NamedColors<TPixel>.Black;

                        var center = new SixLabors.Primitives.Point(image.Width / 2, image.Height / 2);

                        double rotation = (Math.PI * rotationInDegree) / 180.0;
                        double cos = Math.Cos(rotation);
                        double sin = Math.Sin(rotation);

                        int offsetY = image.Height / 6;
                        int axisX = center.X + (int)-(offsetY * sin);
                        int axisY = center.Y + (int)(offsetY * cos);

                        var unicolorLinearGradientBrush = new EllipticGradientBrush<TPixel>(
                            center,
                            new SixLabors.Primitives.Point(axisX, axisY),
                            ratio,
                            GradientRepetitionMode.None,
                            new ColorStop<TPixel>(0, yellow),
                            new ColorStop<TPixel>(1, red),
                            new ColorStop<TPixel>(1, black));

                        image.Mutate(x => x.Fill(unicolorLinearGradientBrush));
                    },
                variant,
                false,
                false);
        }
    }
}