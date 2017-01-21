// ReSharper disable InconsistentNaming
namespace ImageSharp.Tests
{
    using System;
    using System.Numerics;

    using ImageSharp.Formats.Jpg;

    using Xunit;

    public class JpegUtilsTests : TestBase
    {
        public static Image<TColor> CreateTestImage<TColor>(GenericFactory<TColor> factory)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            Image<TColor> image = factory.CreateImage(10, 10);

            using (PixelAccessor<TColor> pixels = image.Lock())
            {
                for (int i = 0; i < 10; i++)
                {
                    for (int j = 0; j < 10; j++)
                    {
                        Vector4 v = new Vector4(i / 10f, j / 10f, 0, 1);

                        TColor color = default(TColor);
                        color.PackFromVector4(v);

                        pixels[i, j] = color;
                    }
                }
            }
            return image;
        }

        [Theory]
        [WithMemberFactory(nameof(CreateTestImage), PixelTypes.Color | PixelTypes.StandardImageClass | PixelTypes.Argb)]
        public void CopyStretchedRGBTo_FromOrigo<TColor>(TestImageProvider<TColor> provider)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            Image<TColor> src = provider.GetImage();

            PixelArea<TColor> area = new PixelArea<TColor>(8, 8, ComponentOrder.Xyz);
            Image<TColor> dest = provider.Factory.CreateImage(8, 8);

            using (var s = src.Lock())
            {
                using (var d = dest.Lock())
                {
                    s.CopyRGBBytesStretchedTo(area, 0, 0);
                    d.CopyFrom(area, 0, 0);

                    Assert.Equal(s[0, 0], d[0, 0]);
                    Assert.Equal(s[7, 0], d[7, 0]);
                    Assert.Equal(s[0, 7], d[0, 7]);
                    Assert.Equal(s[7, 7], d[7, 7]);
                }
            }
        }

        [Theory]
        [WithMemberFactory(nameof(CreateTestImage), PixelTypes.Color | PixelTypes.StandardImageClass | PixelTypes.Argb)]
        public void CopyStretchedRGBTo_WithOffset<TColor>(TestImageProvider<TColor> provider)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            Image<TColor> src = provider.GetImage();

            PixelArea<TColor> area = new PixelArea<TColor>(8, 8, ComponentOrder.Xyz);
            Image<TColor> dest = provider.Factory.CreateImage(8, 8);

            using (PixelAccessor<TColor> s = src.Lock())
            {
                using (var d = dest.Lock())
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
}