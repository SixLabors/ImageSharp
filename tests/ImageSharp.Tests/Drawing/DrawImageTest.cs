// <copyright file="BlendTest.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System.IO;
    using System.Linq;
    using ImageSharp.PixelFormats;
    using Xunit;

    public class DrawImageTest : FileTestBase
    {
        private const PixelTypes PixelTypes = Tests.PixelTypes.StandardImageClass;

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
                image.DrawImage(blend, mode, .75f, new Size(image.Width / 2, image.Height / 2), new Point(image.Width / 4, image.Height / 4))
                     .DebugSave(provider, new { mode });
            }
        }
    }
}
