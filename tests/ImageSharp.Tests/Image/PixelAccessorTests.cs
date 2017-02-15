// <copyright file="PixelAccessorTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using System;
    using System.Numerics;

    using Xunit;

    /// <summary>
    /// Tests the <see cref="PixelAccessor"/> class.
    /// </summary>
    public class PixelAccessorTests
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
                        Vector4 v = new Vector4(i, j, 0, 1);
                        v /= 10;

                        TColor color = default(TColor);
                        color.PackFromVector4(v);

                        pixels[i, j] = color;
                    }
                }
            }
            return image;
        }

        [Theory]
        [WithMemberFactory(nameof(CreateTestImage), PixelTypes.All, ComponentOrder.Xyz)]
        [WithMemberFactory(nameof(CreateTestImage), PixelTypes.All, ComponentOrder.Zyx)]
        [WithMemberFactory(nameof(CreateTestImage), PixelTypes.All, ComponentOrder.Xyzw)]
        [WithMemberFactory(nameof(CreateTestImage), PixelTypes.All, ComponentOrder.Zyxw)]
        public void CopyTo_Then_CopyFrom_OnFullImageRect<TColor>(TestImageProvider<TColor> provider, ComponentOrder order)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            using (Image<TColor> src = provider.GetImage())
            {
                using (Image<TColor> dest = new Image<TColor>(src.Width, src.Height))
                {
                    using (PixelArea<TColor> area = new PixelArea<TColor>(src.Width, src.Height, order))
                    {
                        using (PixelAccessor<TColor> srcPixels = src.Lock())
                        {
                            srcPixels.CopyTo(area, 0, 0);
                        }

                        using (PixelAccessor<TColor> destPixels = dest.Lock())
                        {
                            destPixels.CopyFrom(area, 0, 0);
                        }
                    }

                    Assert.True(src.IsEquivalentTo(dest, false));
                }
            }
        }

        // TODO: Need a processor in the library with this signature
        private static void Fill<TColor>(Image<TColor> image, Rectangle region, TColor color)
             where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            using (PixelAccessor<TColor> pixels = image.Lock())
            {
                for (int y = region.Top; y < region.Bottom; y++)
                {
                    for (int x = region.Left; x < region.Right; x++)
                    {
                        pixels[x, y] = color;
                    }
                }
            }
        }

        [Theory]
        [WithBlankImages(16, 16, PixelTypes.All, ComponentOrder.Xyz)]
        [WithBlankImages(16, 16, PixelTypes.All, ComponentOrder.Zyx)]
        [WithBlankImages(16, 16, PixelTypes.All, ComponentOrder.Xyzw)]
        [WithBlankImages(16, 16, PixelTypes.All, ComponentOrder.Zyxw)]
        public void CopyToThenCopyFromWithOffset<TColor>(TestImageProvider<TColor> provider, ComponentOrder order)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            using (Image<TColor> destImage = new Image<TColor>(8, 8))
            {
                using (Image<TColor> srcImage = provider.GetImage())
                {
                    Fill(srcImage, new Rectangle(4, 4, 8, 8), NamedColors<TColor>.Red);
                    using (PixelAccessor<TColor> srcPixels = srcImage.Lock())
                    {
                        using (PixelArea<TColor> area = new PixelArea<TColor>(8, 8, order))
                        {
                            srcPixels.CopyTo(area, 4, 4);

                            using (PixelAccessor<TColor> destPixels = destImage.Lock())
                            {
                                destPixels.CopyFrom(area, 0, 0);
                            }
                        }
                    }
                }

                provider.Utility.SourceFileOrDescription = order.ToString();
                provider.Utility.SaveTestOutputFile(destImage, "bmp");

                using (Image<TColor> expectedImage = new Image<TColor>(8, 8).Fill(NamedColors<TColor>.Red))
                {
                    Assert.True(destImage.IsEquivalentTo(expectedImage));
                }
            }
        }


        [Fact]
        public void CopyFromZYX()
        {
            using (Image<Color> image = new Image<Color>(1, 1))
            {
                CopyFromZYX(image);
            }
        }

        [Fact]
        public void CopyFromZYXOptimized()
        {
            using (Image image = new Image(1, 1))
            {
                CopyFromZYX(image);
            }
        }

        [Fact]
        public void CopyFromZYXW()
        {
            using (Image<Color> image = new Image<Color>(1, 1))
            {
                CopyFromZYXW(image);
            }
        }

        [Fact]
        public void CopyFromZYXWOptimized()
        {
            using (Image image = new Image(1, 1))
            {
                CopyFromZYXW(image);
            }
        }

        [Fact]
        public void CopyToZYX()
        {
            using (Image<Color> image = new Image<Color>(1, 1))
            {
                CopyToZYX(image);
            }
        }

        [Fact]
        public void CopyToZYXOptimized()
        {
            using (Image image = new Image(1, 1))
            {
                CopyToZYX(image);
            }
        }

        [Fact]
        public void CopyToZYXW()
        {
            using (Image<Color> image = new Image<Color>(1, 1))
            {
                CopyToZYXW(image);
            }
        }

        [Fact]
        public void CopyToZYXWOptimized()
        {
            using (Image image = new Image(1, 1))
            {
                CopyToZYXW(image);
            }
        }

        private static void CopyFromZYX<TColor>(Image<TColor> image)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            using (PixelAccessor<TColor> pixels = image.Lock())
            {
                byte red = 1;
                byte green = 2;
                byte blue = 3;
                byte alpha = 255;

                using (PixelArea<TColor> row = new PixelArea<TColor>(1, ComponentOrder.Zyx))
                {
                    row.Bytes[0] = blue;
                    row.Bytes[1] = green;
                    row.Bytes[2] = red;

                    pixels.CopyFrom(row, 0);

                    Color color = (Color)(object)pixels[0, 0];
                    Assert.Equal(red, color.R);
                    Assert.Equal(green, color.G);
                    Assert.Equal(blue, color.B);
                    Assert.Equal(alpha, color.A);
                }
            }
        }

        private static void CopyFromZYXW<TColor>(Image<TColor> image)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            using (PixelAccessor<TColor> pixels = image.Lock())
            {
                byte red = 1;
                byte green = 2;
                byte blue = 3;
                byte alpha = 4;

                using (PixelArea<TColor> row = new PixelArea<TColor>(1, ComponentOrder.Zyxw))
                {
                    row.Bytes[0] = blue;
                    row.Bytes[1] = green;
                    row.Bytes[2] = red;
                    row.Bytes[3] = alpha;

                    pixels.CopyFrom(row, 0);

                    Color color = (Color)(object)pixels[0, 0];
                    Assert.Equal(red, color.R);
                    Assert.Equal(green, color.G);
                    Assert.Equal(blue, color.B);
                    Assert.Equal(alpha, color.A);
                }
            }
        }

        private static void CopyToZYX<TColor>(Image<TColor> image)
          where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            using (PixelAccessor<TColor> pixels = image.Lock())
            {
                byte red = 1;
                byte green = 2;
                byte blue = 3;

                using (PixelArea<TColor> row = new PixelArea<TColor>(1, ComponentOrder.Zyx))
                {
                    pixels[0, 0] = (TColor)(object)new Color(red, green, blue);

                    pixels.CopyTo(row, 0);

                    Assert.Equal(blue, row.Bytes[0]);
                    Assert.Equal(green, row.Bytes[1]);
                    Assert.Equal(red, row.Bytes[2]);
                }
            }
        }

        private static void CopyToZYXW<TColor>(Image<TColor> image)
            where TColor : struct, IPackedPixel, IEquatable<TColor>
        {
            using (PixelAccessor<TColor> pixels = image.Lock())
            {
                byte red = 1;
                byte green = 2;
                byte blue = 3;
                byte alpha = 4;

                using (PixelArea<TColor> row = new PixelArea<TColor>(1, ComponentOrder.Zyxw))
                {
                    pixels[0, 0] = (TColor)(object)new Color(red, green, blue, alpha);

                    pixels.CopyTo(row, 0);

                    Assert.Equal(blue, row.Bytes[0]);
                    Assert.Equal(green, row.Bytes[1]);
                    Assert.Equal(red, row.Bytes[2]);
                    Assert.Equal(alpha, row.Bytes[3]);
                }
            }
        }
    }
}
