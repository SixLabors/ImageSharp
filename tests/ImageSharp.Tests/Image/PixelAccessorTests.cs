// <copyright file="PixelAccessorTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

// ReSharper disable InconsistentNaming
namespace ImageSharp.Tests
{
    using System;
    using System.Numerics;

    using ImageSharp.Drawing.Shapes;
    using ImageSharp.Tests.TestUtilities;

    using Xunit;

    /// <summary>
    /// Tests the <see cref="PixelAccessor"/> class.
    /// </summary>
    public class PixelAccessorTests
    {
        public static Image<TColor, TPacked> CreateTestImage<TColor, TPacked>()
            where TColor : struct, IPackedPixel<TPacked> where TPacked : struct, IEquatable<TPacked>
        {
            Image<TColor, TPacked> image = new Image<TColor, TPacked>(10, 10);

            using (var pixels = image.Lock())
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
        [WithMemberFactory(nameof(CreateTestImage), PixelTypes.All, ComponentOrder.XYZ)]
        [WithMemberFactory(nameof(CreateTestImage), PixelTypes.All, ComponentOrder.ZYX)]
        [WithMemberFactory(nameof(CreateTestImage), PixelTypes.All, ComponentOrder.XYZW)]
        [WithMemberFactory(nameof(CreateTestImage), PixelTypes.All, ComponentOrder.ZYXW)]
        public void CopyTo_Then_CopyFrom_OnFullImageRect<TColor, TPacked>(TestImageFactory<TColor, TPacked> factory, ComponentOrder order)
            where TColor : struct, IPackedPixel<TPacked> where TPacked : struct, IEquatable<TPacked>
        {
            var src = factory.Create();

            var dest = new Image<TColor,TPacked>(src.Width, src.Height);

            using (PixelArea<TColor, TPacked> area = new PixelArea<TColor, TPacked>(src.Width, src.Height, order))
            {
                using (var srcPixels = src.Lock())
                {
                    srcPixels.CopyTo(area, 0, 0);
                }

                using (var destPixels = dest.Lock())
                {
                    destPixels.CopyFrom(area, 0, 0);
                }
            }
            
            Assert.True(src.IsEquivalentTo(dest, false));
        }

        // TODO: Need a processor in the library with this signature
        private static void Fill<TColor, TPacked>(Image<TColor, TPacked> image, Rectangle region, TColor color)
             where TColor : struct, IPackedPixel<TPacked> where TPacked : struct, IEquatable<TPacked>
        {
            using (var pixels = image.Lock())
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
        [WithBlankImages(16, 16, PixelTypes.All, ComponentOrder.XYZ)]
        [WithBlankImages(16, 16, PixelTypes.All, ComponentOrder.ZYX)]
        [WithBlankImages(16, 16, PixelTypes.All, ComponentOrder.XYZW)]
        [WithBlankImages(16, 16, PixelTypes.All, ComponentOrder.ZYXW)]
        public void CopyTo_Then_CopyFrom_WithOffset<TColor, TPacked>(TestImageFactory<TColor, TPacked> factory, ComponentOrder order)
            where TColor : struct, IPackedPixel<TPacked> where TPacked : struct, IEquatable<TPacked>

        {
            var srcImage = factory.Create();

            var color = default(TColor);
            color.PackFromBytes(255, 0, 0, 255);

            Fill(srcImage, new Rectangle(4, 4, 8, 8), color);

            var destImage = new Image<TColor, TPacked>(8, 8);

            using (var srcPixels = srcImage.Lock())
            {
                using (var area = new PixelArea<TColor, TPacked>(8, 8, order))
                {
                    srcPixels.CopyTo(area, 4, 4);

                    using (var destPixels = destImage.Lock())
                    {
                        destPixels.CopyFrom(area, 0, 0);
                    }
                }
            }

            factory.Utility.SourceFileOrDescription = order.ToString();
            factory.Utility.SaveTestOutputFile(destImage, "bmp");

            var expectedImage = new Image<TColor, TPacked>(8, 8).Fill(color);
            
            Assert.True(destImage.IsEquivalentTo(expectedImage));
        }



        [Fact]
        public void CopyFromZYX()
        {
            CopyFromZYX(new Image<Color, uint>(1, 1));
        }

        [Fact]
        public void CopyFromZYXOptimized()
        {
            CopyFromZYX(new Image(1, 1));
        }

        [Fact]
        public void CopyFromZYXW()
        {
            CopyFromZYXW(new Image<Color, uint>(1, 1));
        }

        [Fact]
        public void CopyFromZYXWOptimized()
        {
            CopyFromZYXW(new Image(1, 1));
        }

        [Fact]
        public void CopyToZYX()
        {
            CopyToZYX(new Image<Color, uint>(1, 1));
        }
        
        [Fact]
        public void CopyToXYZ()
        {
            CopyToXYZ(new Image<Color, uint>(1, 1));
        }

        [Fact]
        public void CopyToZYXOptimized()
        {
            CopyToZYX(new Image(1, 1));
        }

        [Fact]
        public void CopyToZYXW()
        {
            CopyToZYXW(new Image<Color, uint>(1, 1));
        }

        [Fact]
        public void CopyToZYXWOptimized()
        {
            CopyToZYXW(new Image(1, 1));
        }

        private static void CopyFromZYX<TColor, TPacked>(Image<TColor, TPacked> image)
            where TColor : struct, IPackedPixel<TPacked> where TPacked : struct, IEquatable<TPacked>
        {
            using (PixelAccessor<TColor, TPacked> pixels = image.Lock())
            {
                byte red = 1;
                byte green = 2;
                byte blue = 3;
                byte alpha = 255;

                using (PixelArea<TColor, TPacked> row = new PixelArea<TColor, TPacked>(1, ComponentOrder.ZYX))
                {
                    row.Bytes[0] = blue;
                    row.Bytes[1] = green;
                    row.Bytes[2] = red;

                    pixels.CopyFrom(row, 0);

                    Color color = (Color) (object) pixels[0, 0];
                    Assert.Equal(red, color.R);
                    Assert.Equal(green, color.G);
                    Assert.Equal(blue, color.B);
                    Assert.Equal(alpha, color.A);
                }
            }
        }

        private static void CopyFromZYXW<TColor, TPacked>(Image<TColor, TPacked> image)
            where TColor : struct, IPackedPixel<TPacked> where TPacked : struct, IEquatable<TPacked>
        {
            using (PixelAccessor<TColor, TPacked> pixels = image.Lock())
            {
                byte red = 1;
                byte green = 2;
                byte blue = 3;
                byte alpha = 4;

                using (PixelArea<TColor, TPacked> row = new PixelArea<TColor, TPacked>(1, ComponentOrder.ZYXW))
                {
                    row.Bytes[0] = blue;
                    row.Bytes[1] = green;
                    row.Bytes[2] = red;
                    row.Bytes[3] = alpha;

                    pixels.CopyFrom(row, 0);

                    Color color = (Color) (object) pixels[0, 0];
                    Assert.Equal(red, color.R);
                    Assert.Equal(green, color.G);
                    Assert.Equal(blue, color.B);
                    Assert.Equal(alpha, color.A);
                }
            }
        }

        private static void CopyToZYX<TColor, TPacked>(Image<TColor, TPacked> image)
          where TColor : struct, IPackedPixel<TPacked> where TPacked : struct, IEquatable<TPacked>
        {
            using (PixelAccessor<TColor, TPacked> pixels = image.Lock())
            {
                byte red = 1;
                byte green = 2;
                byte blue = 3;

                using (PixelArea<TColor, TPacked> row = new PixelArea<TColor, TPacked>(1, ComponentOrder.ZYX))
                {
                    pixels[0, 0] = (TColor) (object) new Color(red, green, blue);

                    pixels.CopyTo(row, 0);

                    Assert.Equal(blue, row.Bytes[0]);
                    Assert.Equal(green, row.Bytes[1]);
                    Assert.Equal(red, row.Bytes[2]);
                }
            }
        }

        private static void CopyToXYZ<TColor, TPacked>(Image<TColor, TPacked> image)
          where TColor : struct, IPackedPixel<TPacked> where TPacked : struct, IEquatable<TPacked>
        {
            using (PixelAccessor<TColor, TPacked> pixels = image.Lock())
            {
                byte red = 1;
                byte green = 2;
                byte blue = 3;

                using (PixelArea<TColor, TPacked> row = new PixelArea<TColor, TPacked>(image.Width, image.Height, ComponentOrder.XYZ))
                {
                    pixels[0, 0] = (TColor)(object)new Color(red, green, blue);

                    pixels.CopyTo(row, 0);

                    Assert.Equal(red, row.Bytes[0]);
                    Assert.Equal(green, row.Bytes[1]);
                    Assert.Equal(blue, row.Bytes[2]);
                }
            }
        }


        private static void CopyToZYXW<TColor, TPacked>(Image<TColor, TPacked> image)
          where TColor : struct, IPackedPixel<TPacked> where TPacked : struct, IEquatable<TPacked>
        {
            using (PixelAccessor<TColor, TPacked> pixels = image.Lock())
            {
                byte red = 1;
                byte green = 2;
                byte blue = 3;
                byte alpha = 4;

                using (PixelArea<TColor, TPacked> row = new PixelArea<TColor, TPacked>(1, ComponentOrder.ZYXW))
                {
                    pixels[0, 0] = (TColor) (object) new Color(red, green, blue, alpha);

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
