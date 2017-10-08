// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.IO;
using System.Linq;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    using System;

    public class DrawImageTest : FileTestBase
    {
        private const PixelTypes PixelTypes = Tests.PixelTypes.Rgba32;

        public static readonly string[] TestFiles = {
               TestImages.Jpeg.Baseline.Calliphora,
               TestImages.Bmp.Car,
               TestImages.Png.Splash,
               TestImages.Gif.Rings
        };

        object[][] Modes = System.Enum.GetValues(typeof(PixelBlenderMode)).Cast<PixelBlenderMode>().Select(x => new object[] { x }).ToArray();

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
            using (Image<TPixel> blend = Image.Load<TPixel>(TestFile.Create(TestImages.Bmp.Car).Bytes))
            {
                image.Mutate(x => x.DrawImage(blend, mode, .75f, new Size(image.Width / 2, image.Height / 2), new Point(image.Width / 4, image.Height / 4)));
                image.DebugSave(provider, new { mode });
            }
        }

        [Theory]
        [WithFile(TestImages.Bmp.Car, PixelTypes.Rgba32, TestImages.Png.Splash)]
        public void ImageShouldHandleNegativeLocation(TestImageProvider<Rgba32> provider, string backgroundPath)
        {
            using (Image<Rgba32> background = TestFile.Create(backgroundPath).CreateImage())
            using (Image<Rgba32> overlay = provider.GetImage())
            {
                int xy = -25;
                Rgba32 backgroundPixel = background[0, 0];
                Rgba32 overlayPixel = overlay[Math.Abs(xy) + 1, Math.Abs(xy) + 1];

                background.Mutate(x => x.DrawImage(overlay, PixelBlenderMode.Normal, 1F, new Size(overlay.Width, overlay.Height), new Point(xy, xy)));

                Assert.Equal(default(Rgba32), backgroundPixel);
                Assert.Equal(overlayPixel, background[0, 0]);

                background.DebugSave(provider, new[] { "Negative" });
            }
        }

        [Theory]
        [WithFile(TestImages.Bmp.Car, PixelTypes.Rgba32, TestImages.Png.Splash)]
        public void ImageShouldHandlePositiveLocation(TestImageProvider<Rgba32> provider, string backgroundPath)
        {
            using (Image<Rgba32> background = TestFile.Create(backgroundPath).CreateImage())
            using (Image<Rgba32> overlay = provider.GetImage())
            {
                int xy = 25;
                Rgba32 backgroundPixel = background[xy - 1, xy - 1];
                Rgba32 overlayPixel = overlay[0, 0];

                background.Mutate(x => x.DrawImage(overlay, PixelBlenderMode.Normal, 1F, new Size(overlay.Width, overlay.Height), new Point(xy, xy)));

                Assert.Equal(default(Rgba32), backgroundPixel);
                Assert.Equal(overlayPixel, background[xy, xy]);

                background.DebugSave(provider, new[] { "Positive" });
            }
        }
    }
}
