// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
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
            public void Vector4Constructor()
            {
                // Act:
                Color color = new(Vector4.One);

                // Assert:
                Assert.Equal(new RgbaVector(1, 1, 1, 1), color.ToPixel<RgbaVector>());
                Assert.Equal(new Rgba64(65535, 65535, 65535, 65535), color.ToPixel<Rgba64>());
                Assert.Equal(new Rgba32(255, 255, 255, 255), color.ToPixel<Rgba32>());
                Assert.Equal(new L8(255), color.ToPixel<L8>());
            }

            [Fact]
            public void GenericPixelRoundTrip()
            {
                AssertGenericPixelRoundTrip(new RgbaVector(float.Epsilon, 2 * float.Epsilon, float.MaxValue, float.MinValue));
                AssertGenericPixelRoundTrip(new Rgba64(1, 2, ushort.MaxValue, ushort.MaxValue - 1));
                AssertGenericPixelRoundTrip(new Rgb48(1, 2, ushort.MaxValue - 1));
                AssertGenericPixelRoundTrip(new La32(1, ushort.MaxValue - 1));
                AssertGenericPixelRoundTrip(new L16(ushort.MaxValue - 1));
                AssertGenericPixelRoundTrip(new Rgba32(1, 2, 255, 254));
            }

            private static void AssertGenericPixelRoundTrip<TPixel>(TPixel source)
                where TPixel : unmanaged, IPixel<TPixel>
            {
                // Act:
                var color = Color.FromPixel(source);

                // Assert:
                TPixel actual = color.ToPixel<TPixel>();
                Assert.Equal(source, actual);
            }

            [Fact]
            public void GenericPixelDifferentPrecision()
            {
                AssertGenericPixelDifferentPrecision(new RgbaVector(1, 1, 1, 1), new Rgba64(65535, 65535, 65535, 65535));
                AssertGenericPixelDifferentPrecision(new RgbaVector(1, 1, 1, 1), new Rgba32(255, 255, 255, 255));
                AssertGenericPixelDifferentPrecision(new Rgba64(65535, 65535, 65535, 65535), new Rgba32(255, 255, 255, 255));
                AssertGenericPixelDifferentPrecision(new Rgba32(255, 255, 255, 255), new L8(255));
            }

            private static void AssertGenericPixelDifferentPrecision<TPixel, TPixel2>(TPixel source, TPixel2 expected)
                where TPixel : unmanaged, IPixel<TPixel>
                where TPixel2 : unmanaged, IPixel<TPixel2>
            {
                // Act:
                var color = Color.FromPixel(source);

                // Assert:
                TPixel2 actual = color.ToPixel<TPixel2>();
                Assert.Equal(expected, actual);
            }
        }
    }
}
