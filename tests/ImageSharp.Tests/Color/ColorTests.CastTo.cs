// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.PixelFormats;

using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    public partial class ColorTests
    {
        public class CastTo
        {
            [Fact]
            public void Rgba64()
            {
                var source = new Rgba64(100, 2222, 3333, 4444);

                // Act:
                var color = new Color(source);

                // Assert:
                Rgba64 data = color;
                Assert.Equal(source, data);
            }

            [Fact]
            public void Rgba32()
            {
                var source = new Rgba32(1, 22, 33, 231);

                // Act:
                var color = new Color(source);

                // Assert:
                Rgba32 data = color;
                Assert.Equal(source, data);
            }

            [Fact]
            public void Argb32()
            {
                var source = new Argb32(1, 22, 33, 231);

                // Act:
                var color = new Color(source);

                // Assert:
                Argb32 data = color;
                Assert.Equal(source, data);
            }

            [Fact]
            public void Bgra32()
            {
                var source = new Bgra32(1, 22, 33, 231);

                // Act:
                var color = new Color(source);

                // Assert:
                Bgra32 data = color;
                Assert.Equal(source, data);
            }

            [Fact]
            public void Rgb24()
            {
                var source = new Rgb24(1, 22, 231);

                // Act:
                var color = new Color(source);

                // Assert:
                Rgb24 data = color;
                Assert.Equal(source, data);
            }

            [Fact]
            public void Bgr24()
            {
                var source = new Bgr24(1, 22, 231);

                // Act:
                var color = new Color(source);

                // Assert:
                Bgr24 data = color;
                Assert.Equal(source, data);
            }

            [Fact]
            public void GenericPixel()
            {
                AssertGenericPixel(new RgbaVector(float.Epsilon, 2 * float.Epsilon, float.MaxValue, float.MinValue));
                AssertGenericPixel(new Rgba64(1, 2, ushort.MaxValue, ushort.MaxValue - 1));
                AssertGenericPixel(new Rgb48(1, 2, ushort.MaxValue - 1));
                AssertGenericPixel(new La32(1, ushort.MaxValue - 1));
                AssertGenericPixel(new L16(ushort.MaxValue - 1));
                AssertGenericPixel(new Rgba32(1, 2, 255, 254));
            }

            private static void AssertGenericPixel<TPixel>(TPixel source)
                where TPixel : unmanaged, IPixel<TPixel>
            {
                // Act:
                var color = Color.FromPixel(source);

                // Assert:
                TPixel actual = color.ToPixel<TPixel>();
                Assert.Equal(source, actual);
            }
        }
    }
}
