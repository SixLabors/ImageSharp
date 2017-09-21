// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.



// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Tests.Formats.Jpg
{
    using System.Numerics;

    using SixLabors.ImageSharp.Formats.Jpeg.GolangPort.Utils;
    using SixLabors.ImageSharp.PixelFormats;

    using Xunit;

    public class JpegUtilsTests
    {
        public static Image<TPixel> CreateTestImage<TPixel>()
            where TPixel : struct, IPixel<TPixel>
        {
            var image = new Image<TPixel>(10, 10);
            using (PixelAccessor<TPixel> pixels = image.Lock())
            {
                for (int i = 0; i < 10; i++)
                {
                    for (int j = 0; j < 10; j++)
                    {
                        var v = new Vector4(i / 10f, j / 10f, 0, 1);

                        var color = default(TPixel);
                        color.PackFromVector4(v);

                        pixels[i, j] = color;
                    }
                }
            }

            return image;
        }

        [Theory]
        [WithMemberFactory(nameof(CreateTestImage), PixelTypes.Rgba32| PixelTypes.Rgba32 | PixelTypes.Argb32)]
        public void CopyStretchedRGBTo_FromOrigo<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> src = provider.GetImage())
            using (Image<TPixel> dest = new Image<TPixel>(8,8))
            using (PixelArea<TPixel> area = new PixelArea<TPixel>(8, 8, ComponentOrder.Xyz))
            using (PixelAccessor<TPixel> s = src.Lock())
            using (PixelAccessor<TPixel> d = dest.Lock())
            {
                s.CopyRGBBytesStretchedTo(area, 0, 0);
                d.CopyFrom(area, 0, 0);

                Assert.Equal(s[0, 0], d[0, 0]);
                Assert.Equal(s[7, 0], d[7, 0]);
                Assert.Equal(s[0, 7], d[0, 7]);
                Assert.Equal(s[7, 7], d[7, 7]);
            }

        }

        [Theory]
        [WithMemberFactory(nameof(CreateTestImage), PixelTypes.Rgba32| PixelTypes.Rgba32 | PixelTypes.Argb32)]
        public void CopyStretchedRGBTo_WithOffset<TPixel>(TestImageProvider<TPixel> provider)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> src = provider.GetImage())
            using (PixelArea<TPixel> area = new PixelArea<TPixel>(8, 8, ComponentOrder.Xyz))
            using (Image<TPixel> dest = new Image<TPixel>(8, 8))
            using (PixelAccessor<TPixel> s = src.Lock())
            using (PixelAccessor<TPixel> d = dest.Lock())
            {
                s.CopyRGBBytesStretchedTo(area, 7, 6);
                d.CopyFrom(area, 0, 0);

                Assert.Equal(s[6, 7], d[0, 0]);
                Assert.Equal(s[6, 8], d[0, 1]);
                Assert.Equal(s[7, 8], d[1, 1]);

                Assert.Equal(s[6, 9], d[0, 2]);
                Assert.Equal(s[6, 9], d[0, 3]);
                Assert.Equal(s[6, 9], d[0, 7]);

                Assert.Equal(s[7, 9], d[1, 2]);
                Assert.Equal(s[7, 9], d[1, 3]);
                Assert.Equal(s[7, 9], d[1, 7]);

                Assert.Equal(s[9, 9], d[3, 2]);
                Assert.Equal(s[9, 9], d[3, 3]);
                Assert.Equal(s[9, 9], d[3, 7]);

                Assert.Equal(s[9, 7], d[3, 0]);
                Assert.Equal(s[9, 7], d[4, 0]);
                Assert.Equal(s[9, 7], d[7, 0]);

                Assert.Equal(s[9, 9], d[3, 2]);
                Assert.Equal(s[9, 9], d[4, 2]);
                Assert.Equal(s[9, 9], d[7, 2]);

                Assert.Equal(s[9, 9], d[4, 3]);
                Assert.Equal(s[9, 9], d[7, 7]);
            }
        }
    }
}