// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// Tests the <see cref="PixelAccessor"/> class.
    /// </summary>
    public class PixelAccessorTests
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
                        var v = new Vector4(i, j, 0, 1);
                        v /= 10;

                        var color = default(TPixel);
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
        internal void CopyTo_Then_CopyFrom_OnFullImageRect<TPixel>(TestImageProvider<TPixel> provider, ComponentOrder order)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> src = provider.GetImage())
            {
                using (Image<TPixel> dest = new Image<TPixel>(src.Width, src.Height))
                {
                    using (PixelArea<TPixel> area = new PixelArea<TPixel>(src.Width, src.Height, order))
                    {
                        using (PixelAccessor<TPixel> srcPixels = src.Lock())
                        {
                            srcPixels.CopyTo(area, 0, 0);
                        }

                        using (PixelAccessor<TPixel> destPixels = dest.Lock())
                        {
                            destPixels.CopyFrom(area, 0, 0);
                        }
                    }

                    Assert.True(src.IsEquivalentTo(dest, false));
                }
            }
        }

        [Theory]
        [WithBlankImages(16, 16, PixelTypes.All, ComponentOrder.Xyz)]
        [WithBlankImages(16, 16, PixelTypes.All, ComponentOrder.Zyx)]
        [WithBlankImages(16, 16, PixelTypes.All, ComponentOrder.Xyzw)]
        [WithBlankImages(16, 16, PixelTypes.All, ComponentOrder.Zyxw)]
        internal void CopyToThenCopyFromWithOffset<TPixel>(TestImageProvider<TPixel> provider, ComponentOrder order)
            where TPixel : struct, IPixel<TPixel>
        {
            using (Image<TPixel> destImage = new Image<TPixel>(8, 8))
            {
                using (Image<TPixel> srcImage = provider.GetImage())
                {
                    srcImage.Mutate(x => x.Fill(NamedColors<TPixel>.Red, new Rectangle(4, 4, 8, 8)));
                    using (PixelAccessor<TPixel> srcPixels = srcImage.Lock())
                    {
                        using (PixelArea<TPixel> area = new PixelArea<TPixel>(8, 8, order))
                        {
                            srcPixels.CopyTo(area, 4, 4);

                            using (PixelAccessor<TPixel> destPixels = destImage.Lock())
                            {
                                destPixels.CopyFrom(area, 0, 0);
                            }
                        }
                    }
                }

                provider.Utility.SourceFileOrDescription = order.ToString();
                provider.Utility.SaveTestOutputFile(destImage, "bmp");

                using (Image<TPixel> expectedImage = new Image<TPixel>(8, 8))
                {
                    expectedImage.Mutate(x => x.Fill(NamedColors<TPixel>.Red));
                    Assert.True(destImage.IsEquivalentTo(expectedImage));
                }
            }
        }


        [Fact]
        public void CopyFromZYX()
        {
            using (Image<Rgba32> image = new Image<Rgba32>(1, 1))
            {
                CopyFromZYXImpl(image);
            }
        }
        
        [Fact]
        public void CopyFromZYXW()
        {
            using (Image<Rgba32> image = new Image<Rgba32>(1, 1))
            {
                CopyFromZYXWImpl(image);
            }
        }
        
        [Fact]
        public void CopyToZYX()
        {
            using (Image<Rgba32> image = new Image<Rgba32>(1, 1))
            {
                CopyToZYXImpl(image);
            }
        }
        
        [Fact]
        public void CopyToZYXW()
        {
            using (Image<Rgba32> image = new Image<Rgba32>(1, 1))
            {
                CopyToZYXWImpl(image);
            }
        }
        
        private static void CopyFromZYXImpl<TPixel>(Image<TPixel> image)
            where TPixel : struct, IPixel<TPixel>
        {
            using (PixelAccessor<TPixel> pixels = image.Lock())
            {
                byte red = 1;
                byte green = 2;
                byte blue = 3;
                byte alpha = 255;

                using (PixelArea<TPixel> row = new PixelArea<TPixel>(1, ComponentOrder.Zyx))
                {
                    row.Bytes[0] = blue;
                    row.Bytes[1] = green;
                    row.Bytes[2] = red;

                    pixels.CopyFrom(row, 0);

                    Rgba32 color = (Rgba32)(object)pixels[0, 0];
                    Assert.Equal(red, color.R);
                    Assert.Equal(green, color.G);
                    Assert.Equal(blue, color.B);
                    Assert.Equal(alpha, color.A);
                }
            }
        }

        private static void CopyFromZYXWImpl<TPixel>(Image<TPixel> image)
            where TPixel : struct, IPixel<TPixel>
        {
            using (PixelAccessor<TPixel> pixels = image.Lock())
            {
                byte red = 1;
                byte green = 2;
                byte blue = 3;
                byte alpha = 4;

                using (PixelArea<TPixel> row = new PixelArea<TPixel>(1, ComponentOrder.Zyxw))
                {
                    row.Bytes[0] = blue;
                    row.Bytes[1] = green;
                    row.Bytes[2] = red;
                    row.Bytes[3] = alpha;

                    pixels.CopyFrom(row, 0);

                    Rgba32 color = (Rgba32)(object)pixels[0, 0];
                    Assert.Equal(red, color.R);
                    Assert.Equal(green, color.G);
                    Assert.Equal(blue, color.B);
                    Assert.Equal(alpha, color.A);
                }
            }
        }

        private static void CopyToZYXImpl<TPixel>(Image<TPixel> image)
          where TPixel : struct, IPixel<TPixel>
        {
            using (PixelAccessor<TPixel> pixels = image.Lock())
            {
                byte red = 1;
                byte green = 2;
                byte blue = 3;

                using (PixelArea<TPixel> row = new PixelArea<TPixel>(1, ComponentOrder.Zyx))
                {
                    pixels[0, 0] = (TPixel)(object)new Rgba32(red, green, blue);

                    pixels.CopyTo(row, 0);

                    Assert.Equal(blue, row.Bytes[0]);
                    Assert.Equal(green, row.Bytes[1]);
                    Assert.Equal(red, row.Bytes[2]);
                }
            }
        }

        private static void CopyToZYXWImpl<TPixel>(Image<TPixel> image)
            where TPixel : struct, IPixel<TPixel>
        {
            using (PixelAccessor<TPixel> pixels = image.Lock())
            {
                byte red = 1;
                byte green = 2;
                byte blue = 3;
                byte alpha = 4;

                using (PixelArea<TPixel> row = new PixelArea<TPixel>(1, ComponentOrder.Zyxw))
                {
                    pixels[0, 0] = (TPixel)(object)new Rgba32(red, green, blue, alpha);

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
