// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
using SixLabors.Primitives;
using SixLabors.Shapes;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Drawing
{
    [GroupOutput("Drawing/GradientBrushes")]
    public class FillPathGradientBrushTests
    {
        public static ImageComparer TolerantComparer = ImageComparer.TolerantPercentage(0.01f);

        [Theory]
        [WithBlankImages(10, 10, PixelTypes.Rgba32)]
        public void FillRectangleWithDifferentColors<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            provider.VerifyOperation(
                TolerantComparer,
                image =>
                {
                    ILineSegment[] path =
                    {
                        new LinearLineSegment(new PointF(0, 0), new PointF(10, 0)),
                        new LinearLineSegment(new PointF(10, 0), new PointF(10, 10)),
                        new LinearLineSegment(new PointF(10, 10), new PointF(0, 10)),
                        new LinearLineSegment(new PointF(0, 10), new PointF(0, 0))
                    };

                    Color[] colors = { Color.Black, Color.Red, Color.Yellow, Color.Green };

                    var brush = new PathGradientBrush(path, colors);

                    image.Mutate(x => x.Fill(brush));
                    image.DebugSave(provider, appendPixelTypeToFileName: false, appendSourceFileOrDescription: false);
                });
        }

        [Theory]
        [WithBlankImages(10, 10, PixelTypes.Rgba32)]
        public void FillTriangleWithDifferentColors<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            provider.VerifyOperation(
                TolerantComparer,
                image =>
                {
                    ILineSegment[] path =
                    {
                        new LinearLineSegment(new PointF(5, 0), new PointF(10, 10)),
                        new LinearLineSegment(new PointF(10, 10), new PointF(0, 10)),
                        new LinearLineSegment(new PointF(0, 10), new PointF(5, 0))
                    };

                    Color[] colors = { Color.Red, Color.Green, Color.Blue };

                    var brush = new PathGradientBrush(path, colors);

                    image.Mutate(x => x.Fill(brush));
                    image.DebugSave(provider, appendPixelTypeToFileName: false, appendSourceFileOrDescription: false);
                });
        }

        [Theory]
        [WithBlankImages(10, 10, PixelTypes.Rgba32)]
        public void FillRectangleWithSingleColor<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            {
                ILineSegment[] path =
                {
                    new LinearLineSegment(new PointF(0, 0), new PointF(10, 0)),
                    new LinearLineSegment(new PointF(10, 0), new PointF(10, 10)),
                    new LinearLineSegment(new PointF(10, 10), new PointF(0, 10)),
                    new LinearLineSegment(new PointF(0, 10), new PointF(0, 0))
                };

                Color[] colors = { Color.Red };

                var brush = new PathGradientBrush(path, colors);

                image.Mutate(x => x.Fill(brush));

                image.ComparePixelBufferTo(Color.Red);
            }
        }

        [Theory]
        [WithBlankImages(10, 10, PixelTypes.Rgba32)]
        public void ShouldRotateTheColorsWhenThereAreMorePoints<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            provider.VerifyOperation(
                TolerantComparer,
                image =>
                {
                    ILineSegment[] path =
                    {
                        new LinearLineSegment(new PointF(0, 0), new PointF(10, 0)),
                        new LinearLineSegment(new PointF(10, 0), new PointF(10, 10)),
                        new LinearLineSegment(new PointF(10, 10), new PointF(0, 10)),
                        new LinearLineSegment(new PointF(0, 10), new PointF(0, 0))
                    };

                    Color[] colors = { Color.Red, Color.Yellow };

                    var brush = new PathGradientBrush(path, colors);

                    image.Mutate(x => x.Fill(brush));
                    image.DebugSave(provider, appendPixelTypeToFileName: false, appendSourceFileOrDescription: false);
                });
        }

        [Theory]
        [WithBlankImages(10, 10, PixelTypes.Rgba32)]
        public void FillWithCustomCenterColor<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            provider.VerifyOperation(
                TolerantComparer,
                image =>
                {
                    ILineSegment[] path =
                    {
                        new LinearLineSegment(new PointF(0, 0), new PointF(10, 0)),
                        new LinearLineSegment(new PointF(10, 0), new PointF(10, 10)),
                        new LinearLineSegment(new PointF(10, 10), new PointF(0, 10)),
                        new LinearLineSegment(new PointF(0, 10), new PointF(0, 0))
                    };

                    Color[] colors = { Color.Black, Color.Red, Color.Yellow, Color.Green };

                    var brush = new PathGradientBrush(path, colors, Color.White);

                    image.Mutate(x => x.Fill(brush));
                    image.DebugSave(provider, appendPixelTypeToFileName: false, appendSourceFileOrDescription: false);
                });
        }
    }
}
