// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    [Trait("Category", "PixelFormats")]
    public class Bgra5551Tests
    {
        /// <summary>
        /// Tests the equality operators for equality.
        /// </summary>
        [Fact]
        public void AreEqual()
        {
            var color1 = new Bgra5551(0.0f, 0.0f, 0.0f, 0.0f);
            var color2 = new Bgra5551(new Vector4(0.0f));
            var color3 = new Bgra5551(new Vector4(1.0f, 0.0f, 0.0f, 1.0f));
            var color4 = new Bgra5551(1.0f, 0.0f, 0.0f, 1.0f);

            Assert.Equal(color1, color2);
            Assert.Equal(color3, color4);
        }

        /// <summary>
        /// Tests the equality operators for inequality.
        /// </summary>
        [Fact]
        public void AreNotEqual()
        {
            var color1 = new Bgra5551(0.0f, 0.0f, 0.0f, 0.0f);
            var color2 = new Bgra5551(new Vector4(1.0f));
            var color3 = new Bgra5551(new Vector4(1.0f, 0.0f, 0.0f, 1.0f));
            var color4 = new Bgra5551(1.0f, 1.0f, 0.0f, 1.0f);

            Assert.NotEqual(color1, color2);
            Assert.NotEqual(color3, color4);
        }

        [Fact]
        public void Bgra5551_PackedValue()
        {
            float x = 0x1a;
            float y = 0x16;
            float z = 0xd;
            float w = 0x1;
            Assert.Equal(0xeacd, new Bgra5551(x / 0x1f, y / 0x1f, z / 0x1f, w).PackedValue);
            Assert.Equal(3088, new Bgra5551(0.1f, -0.3f, 0.5f, -0.7f).PackedValue);

            Assert.Equal(0xFFFF, new Bgra5551(Vector4.One).PackedValue);
            Assert.Equal(0x7C00, new Bgra5551(Vector4.UnitX).PackedValue);
            Assert.Equal(0x03E0, new Bgra5551(Vector4.UnitY).PackedValue);
            Assert.Equal(0x001F, new Bgra5551(Vector4.UnitZ).PackedValue);
            Assert.Equal(0x8000, new Bgra5551(Vector4.UnitW).PackedValue);

            // Test the limits.
            Assert.Equal(0x0, new Bgra5551(Vector4.Zero).PackedValue);
            Assert.Equal(0xFFFF, new Bgra5551(Vector4.One).PackedValue);
        }

        [Fact]
        public void Bgra5551_ToVector4()
        {
            Assert.Equal(Vector4.Zero, new Bgra5551(Vector4.Zero).ToVector4());
            Assert.Equal(Vector4.One, new Bgra5551(Vector4.One).ToVector4());
        }

        [Fact]
        public void Bgra5551_ToScaledVector4()
        {
            // arrange
            var bgra = new Bgra5551(Vector4.One);

            // act
            Vector4 actual = bgra.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(1, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Bgra5551_ToRgba32()
        {
            // arrange
            var bgra = new Bgra5551(Vector4.One);
            var expected = new Rgba32(Vector4.One);
            var actual = default(Rgba32);

            // act
            bgra.ToRgba32(ref actual);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra5551_FromScaledVector4()
        {
            // arrange
            Vector4 scaled = new Bgra5551(Vector4.One).ToScaledVector4();
            int expected = 0xFFFF;
            var pixel = default(Bgra5551);

            // act
            pixel.FromScaledVector4(scaled);
            ushort actual = pixel.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra5551_FromBgra5551()
        {
            // arrange
            var bgra = default(Bgra5551);
            var actual = default(Bgra5551);
            var expected = new Bgra5551(1.0f, 0.0f, 1.0f, 1.0f);

            // act
            bgra.FromBgra5551(expected);
            actual.FromBgra5551(bgra);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra5551_FromRgba32()
        {
            // arrange
            var bgra1 = default(Bgra5551);
            var bgra2 = default(Bgra5551);
            ushort expectedPackedValue1 = ushort.MaxValue;
            ushort expectedPackedValue2 = 0xFC1F;

            // act
            bgra1.FromRgba32(new Rgba32(255, 255, 255, 255));
            bgra2.FromRgba32(new Rgba32(255, 0, 255, 255));

            // assert
            Assert.Equal(expectedPackedValue1, bgra1.PackedValue);
            Assert.Equal(expectedPackedValue2, bgra2.PackedValue);
        }

        [Fact]
        public void Bgra5551_FromBgra32()
        {
            // arrange
            var bgra1 = default(Bgra5551);
            var bgra2 = default(Bgra5551);
            ushort expectedPackedValue1 = ushort.MaxValue;
            ushort expectedPackedValue2 = 0xFC1F;

            // act
            bgra1.FromBgra32(new Bgra32(255, 255, 255, 255));
            bgra2.FromBgra32(new Bgra32(255, 0, 255, 255));

            // assert
            Assert.Equal(expectedPackedValue1, bgra1.PackedValue);
            Assert.Equal(expectedPackedValue2, bgra2.PackedValue);
        }

        [Fact]
        public void Bgra5551_FromArgb32()
        {
            // arrange
            var bgra = default(Bgra5551);
            ushort expectedPackedValue = ushort.MaxValue;

            // act
            bgra.FromArgb32(new Argb32(255, 255, 255, 255));

            // assert
            Assert.Equal(expectedPackedValue, bgra.PackedValue);
        }

        [Fact]
        public void Bgra5551_FromRgb48()
        {
            // arrange
            var bgra = default(Bgra5551);
            ushort expectedPackedValue = ushort.MaxValue;

            // act
            bgra.FromRgb48(new Rgb48(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue));

            // assert
            Assert.Equal(expectedPackedValue, bgra.PackedValue);
        }

        [Fact]
        public void Bgra5551_FromRgba64()
        {
            // arrange
            var bgra = default(Bgra5551);
            ushort expectedPackedValue = ushort.MaxValue;

            // act
            bgra.FromRgba64(new Rgba64(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue));

            // assert
            Assert.Equal(expectedPackedValue, bgra.PackedValue);
        }

        [Fact]
        public void Bgra5551_FromGrey16()
        {
            // arrange
            var bgra = default(Bgra5551);
            ushort expectedPackedValue = ushort.MaxValue;

            // act
            bgra.FromL16(new L16(ushort.MaxValue));

            // assert
            Assert.Equal(expectedPackedValue, bgra.PackedValue);
        }

        [Fact]
        public void Bgra5551_FromGrey8()
        {
            // arrange
            var bgra = default(Bgra5551);
            ushort expectedPackedValue = ushort.MaxValue;

            // act
            bgra.FromL8(new L8(byte.MaxValue));

            // assert
            Assert.Equal(expectedPackedValue, bgra.PackedValue);
        }

        [Fact]
        public void Bgra5551_FromBgr24()
        {
            // arrange
            var bgra = default(Bgra5551);
            ushort expectedPackedValue = ushort.MaxValue;

            // act
            bgra.FromBgr24(new Bgr24(byte.MaxValue, byte.MaxValue, byte.MaxValue));

            // assert
            Assert.Equal(expectedPackedValue, bgra.PackedValue);
        }

        [Fact]
        public void Bgra5551_FromRgb24()
        {
            // arrange
            var bgra = default(Bgra5551);
            ushort expectedPackedValue = ushort.MaxValue;

            // act
            bgra.FromRgb24(new Rgb24(byte.MaxValue, byte.MaxValue, byte.MaxValue));

            // assert
            Assert.Equal(expectedPackedValue, bgra.PackedValue);
        }

        [Fact]
        public void Bgra5551_Clamping()
        {
            Assert.Equal(Vector4.Zero, new Bgra5551(Vector4.One * -1234.0f).ToVector4());
            Assert.Equal(Vector4.One, new Bgra5551(Vector4.One * 1234.0f).ToVector4());
        }
    }
}
