// <copyright file="PixelAccessorTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests
{
    using Xunit;

    /// <summary>
    /// Tests the <see cref="PixelAccessor"/> class.
    /// </summary>
    public class PixelAccessorTests
    {
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
            where TColor : struct, IPackedPixel<TPacked>
        where TPacked : struct
        {
            using (PixelAccessor<TColor, TPacked> pixels = image.Lock())
            {
                byte red = 1;
                byte green = 2;
                byte blue = 3;
                byte alpha = 255;

                using (PixelRow<TColor, TPacked> row = new PixelRow<TColor, TPacked>(1, ComponentOrder.ZYX))
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
            where TColor : struct, IPackedPixel<TPacked>
        where TPacked : struct
        {
            using (PixelAccessor<TColor, TPacked> pixels = image.Lock())
            {
                byte red = 1;
                byte green = 2;
                byte blue = 3;
                byte alpha = 4;

                using (PixelRow<TColor, TPacked> row = new PixelRow<TColor, TPacked>(1, ComponentOrder.ZYXW))
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
          where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            using (PixelAccessor<TColor, TPacked> pixels = image.Lock())
            {
                byte red = 1;
                byte green = 2;
                byte blue = 3;

                using (PixelRow<TColor, TPacked> row = new PixelRow<TColor, TPacked>(1, ComponentOrder.ZYX))
                {
                    pixels[0, 0] = (TColor) (object) new Color(red, green, blue);

                    pixels.CopyTo(row, 0);

                    Assert.Equal(blue, row.Bytes[0]);
                    Assert.Equal(green, row.Bytes[1]);
                    Assert.Equal(red, row.Bytes[2]);
                }
            }
        }

        private static void CopyToZYXW<TColor, TPacked>(Image<TColor, TPacked> image)
          where TColor : struct, IPackedPixel<TPacked>
            where TPacked : struct
        {
            using (PixelAccessor<TColor, TPacked> pixels = image.Lock())
            {
                byte red = 1;
                byte green = 2;
                byte blue = 3;
                byte alpha = 4;

                using (PixelRow<TColor, TPacked> row = new PixelRow<TColor, TPacked>(1, ComponentOrder.ZYXW))
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
