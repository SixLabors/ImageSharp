// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Globalization;
using System.Linq;
using System.Text;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Drawing;
using SixLabors.ImageSharp.Processing.Drawing.Brushes.GradientBrushes;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing
{
    [GroupOutput("Drawing/GradientBrushes")]
    public class FillLinearGradientBrushTests
    {
        [Theory]
        [WithBlankImages(10, 10, PixelTypes.Rgba32)]
        public void WithEqualColorsReturnsUnicolorImage<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            provider.VerifyOperation(
                image =>
                    {
                        TPixel red = NamedColors<TPixel>.Red;
                        var unicolorLinearGradientBrush = new LinearGradientBrush<TPixel>(
                            new SixLabors.Primitives.Point(0, 0),
                            new SixLabors.Primitives.Point(10, 0),
                            GradientRepetitionMode.None,
                            new ColorStop<TPixel>(0, red),
                            new ColorStop<TPixel>(1, red));

                        image.Mutate(x => x.Fill(unicolorLinearGradientBrush));
                    },
                false,
                false);
        }

        [Theory]
        [WithBlankImages(500, 10, PixelTypes.Rgba32)]
        public void HorizontalReturnsUnicolorColumns<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            provider.VerifyOperation(
                image =>
                    {
                        TPixel red = NamedColors<TPixel>.Red;
                        TPixel yellow = NamedColors<TPixel>.Yellow;

                        LinearGradientBrush<TPixel> unicolorLinearGradientBrush = new LinearGradientBrush<TPixel>(
                            new SixLabors.Primitives.Point(0, 0),
                            new SixLabors.Primitives.Point(image.Width, 0),
                            GradientRepetitionMode.None,
                            new ColorStop<TPixel>(0, red),
                            new ColorStop<TPixel>(1, yellow));

                        image.Mutate(x => x.Fill(unicolorLinearGradientBrush));
                    },
                false,
                false);
        }

        [Theory]
        [WithBlankImages(500, 10, PixelTypes.Rgba32, GradientRepetitionMode.DontFill)]
        [WithBlankImages(500, 10, PixelTypes.Rgba32, GradientRepetitionMode.None)]
        [WithBlankImages(500, 10, PixelTypes.Rgba32, GradientRepetitionMode.Repeat)]
        [WithBlankImages(500, 10, PixelTypes.Rgba32, GradientRepetitionMode.Reflect)]
        public void HorizontalGradientWithRepMode<TPixel>(
            TestImageProvider<TPixel> provider,
            GradientRepetitionMode repetitionMode)
            where TPixel : struct, IPixel<TPixel>
        {
            provider.VerifyOperation(
                image =>
                    {
                        TPixel red = NamedColors<TPixel>.Red;
                        TPixel yellow = NamedColors<TPixel>.Yellow;

                        var unicolorLinearGradientBrush = new LinearGradientBrush<TPixel>(
                            new SixLabors.Primitives.Point(0, 0),
                            new SixLabors.Primitives.Point(image.Width / 10, 0),
                            repetitionMode,
                            new ColorStop<TPixel>(0, red),
                            new ColorStop<TPixel>(1, yellow));

                        image.Mutate(x => x.Fill(unicolorLinearGradientBrush));
                    },
                false,
                false);
        }

        [Theory]
        [WithBlankImages(200, 100, PixelTypes.Rgba32, new[] { 0.5f })]
        [WithBlankImages(200, 100, PixelTypes.Rgba32, new[] { 0.2f, 0.4f, 0.6f, 0.8f })]
        [WithBlankImages(200, 100, PixelTypes.Rgba32, new[] { 0.1f, 0.3f, 0.6f })]
        public void WithDoubledStopsProduceDashedPatterns<TPixel>(
            TestImageProvider<TPixel> provider,
            float[] pattern)
            where TPixel : struct, IPixel<TPixel>
        {
            string variant = string.Join("_", pattern.Select(i => i.ToString(CultureInfo.InvariantCulture)));

            // ensure the input data is valid
            Assert.True(pattern.Length > 0);

            TPixel black = NamedColors<TPixel>.Black;
            TPixel white = NamedColors<TPixel>.White;

            // create the input pattern: 0, followed by each of the arguments twice, followed by 1.0 - toggling black and white.
            ColorStop<TPixel>[] colorStops =
                Enumerable.Repeat(new ColorStop<TPixel>(0, black), 1)
                .Concat(
                        pattern
                            .SelectMany((f, index) => new[]
                                                      {
                                                          new ColorStop<TPixel>(f, index % 2 == 0 ? black : white),
                                                          new ColorStop<TPixel>(f, index % 2 == 0 ? white : black)
                                                      }))
                .Concat(Enumerable.Repeat(new ColorStop<TPixel>(1, pattern.Length % 2 == 0 ? black : white), 1))
                .ToArray();

            using (Image<TPixel> image = provider.GetImage())
            {
                var unicolorLinearGradientBrush =
                    new LinearGradientBrush<TPixel>(
                        new SixLabors.Primitives.Point(0, 0),
                        new SixLabors.Primitives.Point(image.Width, 0),
                        GradientRepetitionMode.None,
                        colorStops);

                image.Mutate(x => x.Fill(unicolorLinearGradientBrush));

                image.DebugSave(
                    provider,
                    variant,
                    appendPixelTypeToFileName: false,
                    appendSourceFileOrDescription: false);

                // the result must be a black and white pattern, no other color should occur:
                Assert.All(
                    Enumerable.Range(0, image.Width).Select(i => image[i, 0]),
                    color => Assert.True(color.Equals(black) || color.Equals(white)));

                image.CompareToReferenceOutput(
                    provider,
                    variant,
                    appendPixelTypeToFileName: false,
                    appendSourceFileOrDescription: false);
            }
        }

        [Theory]
        [WithBlankImages(10, 500, PixelTypes.Rgba32)]
        public void VerticalReturnsUnicolorColumns<TPixel>(
            TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (var image = provider.GetImage())
            {
                int lastRowIndex = image.Height - 1;

                TPixel red = NamedColors<TPixel>.Red;
                TPixel yellow = NamedColors<TPixel>.Yellow;

                LinearGradientBrush<TPixel> unicolorLinearGradientBrush =
                    new LinearGradientBrush<TPixel>(
                        new SixLabors.Primitives.Point(0, 0),
                        new SixLabors.Primitives.Point(0, image.Height),
                        GradientRepetitionMode.None,
                        new ColorStop<TPixel>(0, red),
                        new ColorStop<TPixel>(1, yellow));

                image.Mutate(x => x.Fill(unicolorLinearGradientBrush));
                image.DebugSave(provider);

                Random random = new Random();

                using (PixelAccessor<TPixel> sourcePixels = image.Lock())
                {
                    TPixel firstRowColor = sourcePixels[0, 0];

                    int columnA = random.Next(0, image.Height);
                    int columnB = random.Next(0, image.Height);
                    int columnC = random.Next(0, image.Height);
                    TPixel columnColorA = sourcePixels[0, columnA];
                    TPixel columnColorB = sourcePixels[0, columnB];
                    TPixel columnColorC = sourcePixels[0, columnC];

                    TPixel lastRowColor = sourcePixels[0, lastRowIndex];

                    for (int i = 0; i < image.Width; i++)
                    {
                        // check first and last column, these are known:
                        Assert.Equal(firstRowColor, sourcePixels[i, 0]);
                        Assert.Equal(lastRowColor, sourcePixels[i, lastRowIndex]);

                        // check the random colors:
                        Assert.Equal(columnColorA, sourcePixels[i, columnA]);
                        Assert.Equal(columnColorB, sourcePixels[i, columnB]);
                        Assert.Equal(columnColorC, sourcePixels[i, columnC]);
                    }
                }

                image.CompareToReferenceOutput(provider);
            }
        }

        public enum ImageCorner
        {
            TopLeft = 0,
            TopRight = 1,
            BottomLeft = 2,
            BottomRight = 3
        }

        [Theory]
        [WithBlankImages(500, 500, PixelTypes.Rgba32, ImageCorner.TopLeft)]
        [WithBlankImages(500, 500, PixelTypes.Rgba32, ImageCorner.TopRight)]
        [WithBlankImages(500, 500, PixelTypes.Rgba32, ImageCorner.BottomLeft)]
        [WithBlankImages(500, 500, PixelTypes.Rgba32, ImageCorner.BottomRight)]
        public void DiagonalReturnsCorrectImages<TPixel>(
            TestImageProvider<TPixel> provider,
            ImageCorner startCorner)
            where TPixel : struct, IPixel<TPixel>
        {
            using (var image = provider.GetImage())
            {
                Assert.True(image.Height == image.Width, "For the math check block at the end the image must be squared, but it is not.");

                int startX = (int)startCorner % 2 == 0 ? 0 : image.Width - 1;
                int startY = startCorner > ImageCorner.TopRight ? 0 : image.Height - 1;
                int endX = image.Height - startX - 1;
                int endY = image.Width - startY - 1;

                TPixel red = NamedColors<TPixel>.Red;
                TPixel yellow = NamedColors<TPixel>.Yellow;

                LinearGradientBrush<TPixel> unicolorLinearGradientBrush =
                    new LinearGradientBrush<TPixel>(
                        new SixLabors.Primitives.Point(startX, startY),
                        new SixLabors.Primitives.Point(endX, endY),
                        GradientRepetitionMode.None,
                        new ColorStop<TPixel>(0, red),
                        new ColorStop<TPixel>(1, yellow));

                image.Mutate(x => x.Fill(unicolorLinearGradientBrush));
                image.DebugSave(provider, startCorner);

                int verticalSign = startY == 0 ? 1 : -1;
                int horizontalSign = startX == 0 ? 1 : -1;

                using (PixelAccessor<TPixel> sourcePixels = image.Lock())
                {
                    // check first and last pixel, these are known:
                    Assert.Equal(red, sourcePixels[startX, startY]);
                    Assert.Equal(yellow, sourcePixels[endX, endY]);

                    for (int i = 0; i < image.Height; i++)
                    {
                        // it's diagonal, so for any (a, a) on the gradient line, for all (a-x, b+x) - +/- depending on the diagonal direction - must be the same color)
                        TPixel colorOnDiagonal = sourcePixels[i, i];
                        int orthoCount = 0;
                        for (int offset = -orthoCount; offset < orthoCount; offset++)
                        {
                            Assert.Equal(colorOnDiagonal, sourcePixels[i + horizontalSign * offset, i + verticalSign * offset]);
                        }
                    }
                }

                image.CompareToReferenceOutput(provider, startCorner);
            }
        }

        [Theory]
        [WithBlankImages(500, 500, PixelTypes.Rgba32, 0, 0, 499, 499, new[] { 0f, .2f, .5f, .9f }, new[] { 0, 0, 1, 1 })]
        [WithBlankImages(500, 500, PixelTypes.Rgba32, 0, 499, 499, 0, new[] { 0f, 0.2f, 0.5f, 0.9f }, new[] { 0, 1, 2, 3 })]
        [WithBlankImages(500, 500, PixelTypes.Rgba32, 499, 499, 0, 0, new[] { 0f, 0.7f, 0.8f, 0.9f}, new[] { 0, 1, 2, 0 })]
        [WithBlankImages(500, 500, PixelTypes.Rgba32, 0, 0, 499, 499, new[] { 0f, .5f, 1f}, new[]{0, 1, 3})]
        public void ArbitraryGradients<TPixel>(
            TestImageProvider<TPixel> provider,
            int startX, int startY,
            int endX, int endY,
            float[] stopPositions,
            int[] stopColorCodes)
            where TPixel : struct, IPixel<TPixel>
        {
            var colors = new []
                             {
                                 NamedColors<TPixel>.Navy,
                                 NamedColors<TPixel>.LightGreen,
                                 NamedColors<TPixel>.Yellow,
                                 NamedColors<TPixel>.Red
                             };

            StringBuilder coloringVariant = new StringBuilder();
            var colorStops = new ColorStop<TPixel>[stopPositions.Length];
            for (int i = 0; i < stopPositions.Length; i++)
            {
                TPixel color = colors[stopColorCodes[i % colors.Length]];
                float position = stopPositions[i];

                colorStops[i] = new ColorStop<TPixel>(
                    position,
                    color);
                coloringVariant.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "{0}@{1};",
                    color,
                    position);
            }

            string variant = $"{startX},{startY}to{endX},{endY};[{coloringVariant}]";

            using (var image = provider.GetImage())
            {
                LinearGradientBrush<TPixel> unicolorLinearGradientBrush =
                    new LinearGradientBrush<TPixel>(
                        new SixLabors.Primitives.Point(startX, startY),
                        new SixLabors.Primitives.Point(endX, endY),
                        GradientRepetitionMode.None,
                        colorStops);

                image.Mutate(x => x.Fill(unicolorLinearGradientBrush));
                image.DebugSave(provider, variant);
                image.CompareToReferenceOutput(provider, variant);
            }
        }
    }
}