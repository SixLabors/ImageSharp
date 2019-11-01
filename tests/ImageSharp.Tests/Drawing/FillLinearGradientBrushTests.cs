﻿// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Globalization;
using System.Linq;
using System.Text;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using SixLabors.Shapes;

using Xunit;
// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Tests.Drawing
{
    using SixLabors.ImageSharp.Advanced;
    using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

    [GroupOutput("Drawing/GradientBrushes")]
    public class FillLinearGradientBrushTests
    {
        public static ImageComparer TolerantComparer = ImageComparer.TolerantPercentage(0.01f);

        [Theory]
        [WithBlankImages(10, 10, PixelTypes.Rgba32)]
        public void WithEqualColorsReturnsUnicolorImage<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                Color red = Color.Red;

                var unicolorLinearGradientBrush = new LinearGradientBrush(
                    new SixLabors.Primitives.Point(0, 0),
                    new SixLabors.Primitives.Point(10, 0),
                    GradientRepetitionMode.None,
                    new ColorStop(0, red),
                    new ColorStop(1, red));

                image.Mutate(x => x.Fill(unicolorLinearGradientBrush));

                image.DebugSave(provider, appendPixelTypeToFileName: false, appendSourceFileOrDescription: false);

                // no need for reference image in this test:
                image.ComparePixelBufferTo(red);
            }
        }

        [Theory]
        [WithBlankImages(20, 10, PixelTypes.Rgba32)]
        [WithBlankImages(20, 10, PixelTypes.Argb32)]
        [WithBlankImages(20, 10, PixelTypes.Rgb24)]
        public void DoesNotDependOnSinglePixelType<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            provider.VerifyOperation(
                TolerantComparer,
                image =>
                    {
                        var unicolorLinearGradientBrush = new LinearGradientBrush(
                            new SixLabors.Primitives.Point(0, 0),
                            new SixLabors.Primitives.Point(image.Width, 0),
                            GradientRepetitionMode.None,
                            new ColorStop(0, Color.Blue),
                            new ColorStop(1, Color.Yellow));

                        image.Mutate(x => x.Fill(unicolorLinearGradientBrush));
                    },
                appendSourceFileOrDescription: false);
        }

        [Theory]
        [WithBlankImages(500, 10, PixelTypes.Rgba32)]
        public void HorizontalReturnsUnicolorColumns<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            provider.VerifyOperation(
                TolerantComparer,
                image =>
                    {
                        Color red = Color.Red;
                        Color yellow = Color.Yellow;

                        var unicolorLinearGradientBrush = new LinearGradientBrush(
                            new SixLabors.Primitives.Point(0, 0),
                            new SixLabors.Primitives.Point(image.Width, 0),
                            GradientRepetitionMode.None,
                            new ColorStop(0, red),
                            new ColorStop(1, yellow));

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
                TolerantComparer,
                image =>
                    {
                        Color red = Color.Red;
                        Color yellow = Color.Yellow;

                        var unicolorLinearGradientBrush = new LinearGradientBrush(
                            new SixLabors.Primitives.Point(0, 0),
                            new SixLabors.Primitives.Point(image.Width / 10, 0),
                            repetitionMode,
                            new ColorStop(0, red),
                            new ColorStop(1, yellow));

                        image.Mutate(x => x.Fill(unicolorLinearGradientBrush));
                    },
                $"{repetitionMode}",
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

            Color black = Color.Black;
            Color white = Color.White;

            // create the input pattern: 0, followed by each of the arguments twice, followed by 1.0 - toggling black and white.
            ColorStop[] colorStops =
                Enumerable.Repeat(new ColorStop(0, black), 1)
                .Concat(
                        pattern
                            .SelectMany((f, index) => new[]
                                                      {
                                                          new ColorStop(f, index % 2 == 0 ? black : white),
                                                          new ColorStop(f, index % 2 == 0 ? white : black)
                                                      }))
                .Concat(Enumerable.Repeat(new ColorStop(1, pattern.Length % 2 == 0 ? black : white), 1))
                .ToArray();

            using (Image<TPixel> image = provider.GetImage())
            {
                var unicolorLinearGradientBrush =
                    new LinearGradientBrush(
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
                    color => Assert.True(
                        color.Equals(black.ToPixel<TPixel>()) || color.Equals(white.ToPixel<TPixel>())));

                image.CompareToReferenceOutput(
                    TolerantComparer,
                    provider,
                    variant,
                    appendPixelTypeToFileName: false,
                    appendSourceFileOrDescription: false);
            }
        }

        [Theory]
        [WithBlankImages(10, 500, PixelTypes.Rgba32)]
        public void VerticalBrushReturnsUnicolorRows<TPixel>(
            TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            provider.VerifyOperation(
                image =>
                    {
                        Color red = Color.Red;
                        Color yellow = Color.Yellow;

                        var unicolorLinearGradientBrush = new LinearGradientBrush(
                            new SixLabors.Primitives.Point(0, 0),
                            new SixLabors.Primitives.Point(0, image.Height),
                            GradientRepetitionMode.None,
                            new ColorStop(0, red),
                            new ColorStop(1, yellow));

                        image.Mutate(x => x.Fill(unicolorLinearGradientBrush));

                        VerifyAllRowsAreUnicolor(image);
                    },
                false,
                false);

            void VerifyAllRowsAreUnicolor(Image<TPixel> image)
            {
                for (int y = 0; y < image.Height; y++)
                {
                    Span<TPixel> row = image.GetPixelRowSpan(y);
                    TPixel firstColorOfRow = row[0];
                    foreach (TPixel p in row)
                    {
                        Assert.Equal(firstColorOfRow, p);
                    }
                }
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
        [WithBlankImages(200, 200, PixelTypes.Rgba32, ImageCorner.TopLeft)]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, ImageCorner.TopRight)]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, ImageCorner.BottomLeft)]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, ImageCorner.BottomRight)]
        public void DiagonalReturnsCorrectImages<TPixel>(
            TestImageProvider<TPixel> provider,
            ImageCorner startCorner)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                Assert.True(image.Height == image.Width, "For the math check block at the end the image must be squared, but it is not.");

                int startX = (int)startCorner % 2 == 0 ? 0 : image.Width - 1;
                int startY = startCorner > ImageCorner.TopRight ? 0 : image.Height - 1;
                int endX = image.Height - startX - 1;
                int endY = image.Width - startY - 1;

                Color red = Color.Red;
                Color yellow = Color.Yellow;

                var unicolorLinearGradientBrush =
                    new LinearGradientBrush(
                        new SixLabors.Primitives.Point(startX, startY),
                        new SixLabors.Primitives.Point(endX, endY),
                        GradientRepetitionMode.None,
                        new ColorStop(0, red),
                        new ColorStop(1, yellow));

                image.Mutate(x => x.Fill(unicolorLinearGradientBrush));
                image.DebugSave(
                    provider,
                    startCorner,
                    appendPixelTypeToFileName: false,
                    appendSourceFileOrDescription: false);

                int verticalSign = startY == 0 ? 1 : -1;
                int horizontalSign = startX == 0 ? 1 : -1;

                for (int i = 0; i < image.Height; i++)
                {
                    // it's diagonal, so for any (a, a) on the gradient line, for all (a-x, b+x) - +/- depending on the diagonal direction - must be the same color)
                    TPixel colorOnDiagonal = image[i, i];

                    // TODO: This is incorrect. from -0 to < 0 ??
                    int orthoCount = 0;
                    for (int offset = -orthoCount; offset < orthoCount; offset++)
                    {
                        Assert.Equal(colorOnDiagonal, image[i + (horizontalSign * offset), i + (verticalSign * offset)]);
                    }
                }

                image.CompareToReferenceOutput(
                    TolerantComparer,
                    provider,
                    startCorner,
                    appendPixelTypeToFileName: false,
                    appendSourceFileOrDescription: false);
            }
        }

        [Theory]
        [WithBlankImages(500, 500, PixelTypes.Rgba32, 0, 0, 499, 499, new[] { 0f, .2f, .5f, .9f }, new[] { 0, 0, 1, 1 })]
        [WithBlankImages(500, 500, PixelTypes.Rgba32, 0, 499, 499, 0, new[] { 0f, 0.2f, 0.5f, 0.9f }, new[] { 0, 1, 2, 3 })]
        [WithBlankImages(500, 500, PixelTypes.Rgba32, 499, 499, 0, 0, new[] { 0f, 0.7f, 0.8f, 0.9f }, new[] { 0, 1, 2, 0 })]
        [WithBlankImages(500, 500, PixelTypes.Rgba32, 0, 0, 499, 499, new[] { 0f, .5f, 1f }, new[] { 0, 1, 3 })]
        public void ArbitraryGradients<TPixel>(
            TestImageProvider<TPixel> provider,
            int startX, int startY,
            int endX, int endY,
            float[] stopPositions,
            int[] stopColorCodes)
            where TPixel : struct, IPixel<TPixel>
        {
            Color[] colors =
            {
                Color.Navy, Color.LightGreen, Color.Yellow,
                Color.Red
            };

            var coloringVariant = new StringBuilder();
            var colorStops = new ColorStop[stopPositions.Length];

            for (int i = 0; i < stopPositions.Length; i++)
            {
                Color color = colors[stopColorCodes[i % colors.Length]];
                float position = stopPositions[i];
                colorStops[i] = new ColorStop(position, color);
                Rgba32 rgba = color;
                coloringVariant.AppendFormat(CultureInfo.InvariantCulture, "{0}@{1};", rgba.ToHex(), position);
            }

            FormattableString variant = $"({startX},{startY})_TO_({endX},{endY})__[{coloringVariant}]";

            provider.VerifyOperation(
                image =>
                {
                    var unicolorLinearGradientBrush = new LinearGradientBrush(
                        new SixLabors.Primitives.Point(startX, startY),
                        new SixLabors.Primitives.Point(endX, endY),
                        GradientRepetitionMode.None,
                        colorStops);

                    image.Mutate(x => x.Fill(unicolorLinearGradientBrush));
                },
                variant,
                false,
                false);
        }

        [Theory]
        [WithBlankImages(200, 200, PixelTypes.Rgba32, 0, 0, 199, 199, new[] { 0f, .25f, .5f, .75f, 1f }, new[] { 0, 1, 2, 3, 4 })]
        public void MultiplePointGradients<TPixel>(
            TestImageProvider<TPixel> provider,
            int startX, int startY,
            int endX, int endY,
            float[] stopPositions,
            int[] stopColorCodes)
            where TPixel : struct, IPixel<TPixel>
        {
            Color[] colors =
            {
                Color.Black, Color.Blue, Color.Red,
                Color.White, Color.Lime
            };

            var coloringVariant = new StringBuilder();
            var colorStops = new ColorStop[stopPositions.Length];

            for (int i = 0; i < stopPositions.Length; i++)
            {
                Color color = colors[stopColorCodes[i % colors.Length]];
                float position = stopPositions[i];
                colorStops[i] = new ColorStop(position, color);
                Rgba32 rgba = color;
                coloringVariant.AppendFormat(CultureInfo.InvariantCulture, "{0}@{1};", rgba.ToHex(), position);
            }

            FormattableString variant = $"({startX},{startY})_TO_({endX},{endY})__[{coloringVariant}]";

            provider.VerifyOperation(
                image =>
                {
                    var unicolorLinearGradientBrush = new LinearGradientBrush(
                        new SixLabors.Primitives.Point(startX, startY),
                        new SixLabors.Primitives.Point(endX, endY),
                        GradientRepetitionMode.None,
                        colorStops);

                    image.Mutate(x => x.Fill(unicolorLinearGradientBrush));
                },
                variant,
                false,
                false);
        }

        [Theory]
        [WithBlankImages(200, 200, PixelTypes.Rgba32)]
        public void GradientsWithTransparencyOnExistingBackground<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            provider.VerifyOperation(
                image =>
                {
                    image.Mutate(i => i.Fill(Color.Red));
                    image.Mutate(ApplyGloss);

                });

            void ApplyGloss(IImageProcessingContext ctx)
            {
                Size size = ctx.GetCurrentSize();
                IPathCollection glossPath = BuildGloss(size.Width, size.Height);
                var graphicsOptions = new GraphicsOptions(true)
                                      {
                                          ColorBlendingMode = PixelColorBlendingMode.Normal,
                                          AlphaCompositionMode = PixelAlphaCompositionMode.SrcAtop
                                      };
                var linearGradientBrush = new LinearGradientBrush(new Point(0, 0), new Point(0, size.Height / 2), GradientRepetitionMode.Repeat, new ColorStop(0, Color.White.WithAlpha(0.5f)), new ColorStop(1, Color.White.WithAlpha(0.25f)));
                ctx.Fill(graphicsOptions, linearGradientBrush, glossPath);
            }

            IPathCollection BuildGloss(int imageWidth, int imageHeight)
            {
                var pathBuilder = new PathBuilder();
                pathBuilder.AddLine(new PointF(0, 0), new PointF(imageWidth, 0));
                pathBuilder.AddLine(new PointF(imageWidth, 0), new PointF(imageWidth, imageHeight * 0.4f));
                pathBuilder.AddBezier(new PointF(imageWidth, imageHeight * 0.4f), new PointF(imageWidth / 2, imageHeight * 0.6f), new PointF(0, imageHeight * 0.4f));
                pathBuilder.CloseFigure();
                return new PathCollection(pathBuilder.Build());
            }
        }

    }
}
