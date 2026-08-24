// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Tests;

public partial class ColorTests
{
    public class CastTo
    {
        [Fact]
        public void Rgba64()
        {
            Rgba64 source = new(100, 2222, 3333, 4444);

            // Act:
            Color color = Color.FromPixel(source);

            // Assert:
            Rgba64 data = color.ToPixel<Rgba64>();
            Assert.Equal(source, data);
        }

        [Fact]
        public void Rgba32()
        {
            Rgba32 source = new(1, 22, 33, 231);

            // Act:
            Color color = Color.FromPixel(source);

            // Assert:
            Rgba32 data = color.ToPixel<Rgba32>();
            Assert.Equal(source, data);
        }

        [Fact]
        public void Argb32()
        {
            Argb32 source = new(1, 22, 33, 231);

            // Act:
            Color color = Color.FromPixel(source);

            // Assert:
            Argb32 data = color.ToPixel<Argb32>();
            Assert.Equal(source, data);
        }

        [Fact]
        public void Bgra32()
        {
            Bgra32 source = new(1, 22, 33, 231);

            // Act:
            Color color = Color.FromPixel(source);

            // Assert:
            Bgra32 data = color.ToPixel<Bgra32>();
            Assert.Equal(source, data);
        }

        [Fact]
        public void Abgr32()
        {
            Abgr32 source = new(1, 22, 33, 231);

            // Act:
            Color color = Color.FromPixel(source);

            // Assert:
            Abgr32 data = color.ToPixel<Abgr32>();
            Assert.Equal(source, data);
        }

        [Fact]
        public void Rgb24()
        {
            Rgb24 source = new(1, 22, 231);

            // Act:
            Color color = Color.FromPixel(source);

            // Assert:
            Rgb24 data = color.ToPixel<Rgb24>();
            Assert.Equal(source, data);
        }

        [Fact]
        public void Bgr24()
        {
            Bgr24 source = new(1, 22, 231);

            // Act:
            Color color = Color.FromPixel(source);

            // Assert:
            Bgr24 data = color.ToPixel<Bgr24>();
            Assert.Equal(source, data);
        }

        [Fact]
        public void Vector4Constructor()
        {
            // Act:
            Color color = Color.FromScaledVector(Vector4.One);

            // Assert:
            Assert.Equal(new RgbaVector(1, 1, 1, 1), color.ToPixel<RgbaVector>());
            Assert.Equal(new Rgba64(65535, 65535, 65535, 65535), color.ToPixel<Rgba64>());
            Assert.Equal(new Rgba32(255, 255, 255, 255), color.ToPixel<Rgba32>());
            Assert.Equal(new L8(255), color.ToPixel<L8>());
        }

        [Fact]
        public void AssociatedVectorConstructor()
        {
            Rgba32P expected = new(64, 32, 16, 128);

            // Act:
            Color color = Color.FromScaledVector(expected.ToScaledVector4(), PixelAlphaRepresentation.Associated);

            // Assert:
            Assert.Equal(PixelAlphaRepresentation.Associated, color.AlphaRepresentation);
            Assert.Equal(expected.ToScaledVector4(), color.ToScaledVector4());
            Assert.Equal(expected, color.ToPixel<Rgba32P>());
            Assert.Equal(new Rgba32(128, 64, 32, 128), color.ToPixel<Rgba32>());
        }

        [Fact]
        public void UnassociatedPixelToAssociatedPixel()
        {
            Color color = Color.FromPixel(new Rgba32(128, 64, 32, 128));

            // Act:
            Rgba32P actual = color.ToPixel<Rgba32P>();

            // Assert:
            Assert.Equal(new Rgba32P(64, 32, 16, 128), actual);
        }

        [Fact]
        public void AssociatedVectorSpanConstructor()
        {
            Rgba32P expected = new(64, 32, 16, 128);
            Vector4[] source = [expected.ToScaledVector4()];
            Color[] destination = new Color[source.Length];

            // Act:
            Color.FromScaledVector(source, destination, PixelAlphaRepresentation.Associated);

            // Assert:
            Assert.Equal(PixelAlphaRepresentation.Associated, destination[0].AlphaRepresentation);
            Assert.Equal(expected.ToScaledVector4(), destination[0].ToScaledVector4());
            Assert.Equal(expected, destination[0].ToPixel<Rgba32P>());
        }

        [Fact]
        public void AssociatedPixelSpanRoundTrip()
        {
            Rgba32P[] source = [new(64, 32, 16, 128), new(24, 12, 6, 96)];
            Color[] colors = new Color[source.Length];
            Rgba32P[] destination = new Rgba32P[source.Length];

            // Act:
            Color.FromPixel<Rgba32P>(source, colors);
            Color.ToPixel<Rgba32P>(colors, destination);

            // Assert:
            Assert.Equal(source, destination);
            Assert.All(colors, color => Assert.Equal(PixelAlphaRepresentation.Associated, color.AlphaRepresentation));
        }

        [Fact]
        public void GenericPixelRoundTrip()
        {
            AssertGenericPixelRoundTrip(new RgbaVector(0.5f, 0.75f, 1, 0));
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
            Color color = Color.FromPixel(source);

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
            Color color = Color.FromPixel(source);

            // Assert:
            TPixel2 actual = color.ToPixel<TPixel2>();
            Assert.Equal(expected, actual);
        }
    }
}
