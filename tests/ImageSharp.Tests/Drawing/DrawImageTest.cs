// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.Helpers;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    using SixLabors.ImageSharp.Processing.Transforms;

    public class DrawImageTest : FileTestBase
    {
        private const PixelTypes PixelTypes = Tests.PixelTypes.Rgba32;

        public static readonly string[] TestFiles = {
               TestImages.Jpeg.Baseline.Calliphora,
               TestImages.Bmp.Car,
               TestImages.Png.Splash,
               TestImages.Gif.Rings
        };

        [Theory]
        [WithFileCollection(nameof(TestFiles), PixelTypes, PixelBlenderMode.Normal)]
        [WithFileCollection(nameof(TestFiles), PixelTypes, PixelBlenderMode.Multiply)]
        [WithFileCollection(nameof(TestFiles), PixelTypes, PixelBlenderMode.Add)]
        [WithFileCollection(nameof(TestFiles), PixelTypes, PixelBlenderMode.Substract)]
        [WithFileCollection(nameof(TestFiles), PixelTypes, PixelBlenderMode.Screen)]
        [WithFileCollection(nameof(TestFiles), PixelTypes, PixelBlenderMode.Darken)]
        [WithFileCollection(nameof(TestFiles), PixelTypes, PixelBlenderMode.Lighten)]
        [WithFileCollection(nameof(TestFiles), PixelTypes, PixelBlenderMode.Overlay)]
        [WithFileCollection(nameof(TestFiles), PixelTypes, PixelBlenderMode.HardLight)]
        public void ImageShouldApplyDrawImage<TPixel>(TestImageProvider<TPixel> provider, PixelBlenderMode mode)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            using (var blend = Image.Load<TPixel>(TestFile.Create(TestImages.Bmp.Car).Bytes))
            {
                blend.Mutate(x => x.Resize(image.Width / 2, image.Height / 2));
                image.Mutate(x => x.DrawImage(blend, mode, .75f, new Point(image.Width / 4, image.Height / 4)));
                image.DebugSave(provider, new { mode });
            }
        }

        [Theory]
        [WithFileCollection(nameof(TestFiles), PixelTypes, PixelBlenderMode.Normal)]
        public void ImageShouldDrawTransformedImage<TPixel>(TestImageProvider<TPixel> provider, PixelBlenderMode mode)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> image = provider.GetImage())
            using (var blend = Image.Load<TPixel>(TestFile.Create(TestImages.Bmp.Car).Bytes))
            {
                Matrix3x2 rotate = Matrix3x2Extensions.CreateRotationDegrees(45F);
                Matrix3x2 scale = Matrix3x2Extensions.CreateScale(new SizeF(.25F, .25F));
                Matrix3x2 matrix = rotate * scale;

                // Lets center the matrix so we can tell whether any cut-off issues we may have belong to the drawing processor
                Rectangle srcBounds = blend.Bounds();
                Rectangle destBounds = TransformHelpers.GetTransformedBoundingRectangle(srcBounds, matrix);
                Matrix3x2 centeredMatrix = TransformHelpers.GetCenteredTransformMatrix(srcBounds, destBounds, matrix);

                // We pass a new rectangle here based on the dest bounds since we've offset the matrix
                blend.Mutate(x => x.Transform(
                    centeredMatrix,
                    Resamplers.Bicubic,
                    new Rectangle(0, 0, destBounds.Width, destBounds.Height)));

                var position = new Point((image.Width - blend.Width) / 2, (image.Height - blend.Height) / 2);
                image.Mutate(x => x.DrawImage(blend, mode, .75F, position));
                image.DebugSave(provider, new[] { "Transformed" });
            }
        }

        [Theory]
        [WithSolidFilledImages(100, 100, 255, 255, 255, PixelTypes.Rgba32)]
        public void ImageShouldHandleNegativeLocation(TestImageProvider<Rgba32> provider)
        {
            using (Image<Rgba32> background = provider.GetImage())
            using (var overlay = new Image<Rgba32>(50, 50))
            {
                overlay.Mutate(x => x.Fill(Rgba32.Black));

                int xy = -25;
                Rgba32 backgroundPixel = background[0, 0];
                Rgba32 overlayPixel = overlay[Math.Abs(xy) + 1, Math.Abs(xy) + 1];

                background.Mutate(x => x.DrawImage(overlay, PixelBlenderMode.Normal, 1F, new Point(xy, xy)));

                Assert.Equal(Rgba32.White, backgroundPixel);
                Assert.Equal(overlayPixel, background[0, 0]);

                background.DebugSave(provider, new[] { "Negative" });
            }
        }

        [Theory]
        [WithSolidFilledImages(100, 100, 255, 255, 255, PixelTypes.Rgba32)]
        public void ImageShouldHandlePositiveLocation(TestImageProvider<Rgba32> provider)
        {
            using (Image<Rgba32> background = provider.GetImage())
            using (var overlay = new Image<Rgba32>(50, 50))
            {
                overlay.Mutate(x => x.Fill(Rgba32.Black));

                int xy = 25;
                Rgba32 backgroundPixel = background[xy - 1, xy - 1];
                Rgba32 overlayPixel = overlay[0, 0];

                background.Mutate(x => x.DrawImage(overlay, PixelBlenderMode.Normal, 1F, new Point(xy, xy)));

                Assert.Equal(Rgba32.White, backgroundPixel);
                Assert.Equal(overlayPixel, background[xy, xy]);

                background.DebugSave(provider, new[] { "Positive" });
            }
        }
    }
}