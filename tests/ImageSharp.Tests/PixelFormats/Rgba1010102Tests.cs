// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    [Trait("Category", "PixelFormats")]
    public class Rgba1010102Tests
    {
        /// <summary>
        /// Tests the equality operators for equality.
        /// </summary>
        [Fact]
        public void AreEqual()
        {
            var color1 = new Rgba1010102(0.0f, 0.0f, 0.0f, 0.0f);
            var color2 = new Rgba1010102(new Vector4(0.0f));
            var color3 = new Rgba1010102(new Vector4(1.0f, 0.0f, 1.0f, 1.0f));
            var color4 = new Rgba1010102(1.0f, 0.0f, 1.0f, 1.0f);

            Assert.Equal(color1, color2);
            Assert.Equal(color3, color4);
        }

        /// <summary>
        /// Tests the equality operators for inequality.
        /// </summary>
        [Fact]
        public void AreNotEqual()
        {
            var color1 = new Rgba1010102(0.0f, 0.0f, 0.0f, 0.0f);
            var color2 = new Rgba1010102(new Vector4(1.0f));
            var color3 = new Rgba1010102(new Vector4(1.0f, 0.0f, 0.0f, 1.0f));
            var color4 = new Rgba1010102(1.0f, 1.0f, 0.0f, 1.0f);

            Assert.NotEqual(color1, color2);
            Assert.NotEqual(color3, color4);
        }

        [Fact]
        public void Rgba1010102_PackedValue()
        {
            float x = 0x2db;
            float y = 0x36d;
            float z = 0x3b7;
            float w = 0x1;
            Assert.Equal(0x7B7DB6DBU, new Rgba1010102(x / 0x3ff, y / 0x3ff, z / 0x3ff, w / 3).PackedValue);

            Assert.Equal(536871014U, new Rgba1010102(0.1f, -0.3f, 0.5f, -0.7f).PackedValue);

            // Test the limits.
            Assert.Equal(0x0U, new Rgba1010102(Vector4.Zero).PackedValue);
            Assert.Equal(0xFFFFFFFF, new Rgba1010102(Vector4.One).PackedValue);
        }

        [Fact]
        public void Rgba1010102_ToVector4()
        {
            Assert.Equal(Vector4.Zero, new Rgba1010102(Vector4.Zero).ToVector4());
            Assert.Equal(Vector4.One, new Rgba1010102(Vector4.One).ToVector4());
        }

        [Fact]
        public void Rgba1010102_ToScaledVector4()
        {
            // arrange
            var rgba = new Rgba1010102(Vector4.One);

            // act
            Vector4 actual = rgba.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(1, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Rgba1010102_FromScaledVector4()
        {
            // arrange
            var rgba = new Rgba1010102(Vector4.One);
            var actual = default(Rgba1010102);
            uint expected = 0xFFFFFFFF;

            // act
            Vector4 scaled = rgba.ToScaledVector4();
            actual.FromScaledVector4(scaled);

            // assert
            Assert.Equal(expected, actual.PackedValue);
        }

        [Fact]
        public void Rgba1010102_FromBgra5551()
        {
            // arrange
            var rgba = new Rgba1010102(Vector4.One);
            uint expected = 0xFFFFFFFF;

            // act
            rgba.FromBgra5551(new Bgra5551(1.0f, 1.0f, 1.0f, 1.0f));

            // assert
            Assert.Equal(expected, rgba.PackedValue);
        }

        [Fact]
        public void Rgba1010102_FromArgb32()
        {
            // arrange
            var rgba = default(Rgba1010102);
            uint expectedPackedValue = uint.MaxValue;

            // act
            rgba.FromArgb32(new Argb32(255, 255, 255, 255));

            // assert
            Assert.Equal(expectedPackedValue, rgba.PackedValue);
        }

        [Fact]
        public void Rgba1010102_FromRgba32()
        {
            // arrange
            var rgba1 = default(Rgba1010102);
            var rgba2 = default(Rgba1010102);
            uint expectedPackedValue1 = uint.MaxValue;
            uint expectedPackedValue2 = 0xFFF003FF;

            // act
            rgba1.FromRgba32(new Rgba32(255, 255, 255, 255));
            rgba2.FromRgba32(new Rgba32(255, 0, 255, 255));

            // assert
            Assert.Equal(expectedPackedValue1, rgba1.PackedValue);
            Assert.Equal(expectedPackedValue2, rgba2.PackedValue);
        }

        [Fact]
        public void Rgba1010102_FromBgr24()
        {
            // arrange
            var rgba = default(Rgba1010102);
            uint expectedPackedValue = uint.MaxValue;

            // act
            rgba.FromBgr24(new Bgr24(byte.MaxValue, byte.MaxValue, byte.MaxValue));

            // assert
            Assert.Equal(expectedPackedValue, rgba.PackedValue);
        }

        [Fact]
        public void Rgba1010102_FromGrey8()
        {
            // arrange
            var rgba = default(Rgba1010102);
            uint expectedPackedValue = uint.MaxValue;

            // act
            rgba.FromL8(new L8(byte.MaxValue));

            // assert
            Assert.Equal(expectedPackedValue, rgba.PackedValue);
        }

        [Fact]
        public void Rgba1010102_FromGrey16()
        {
            // arrange
            var rgba = default(Rgba1010102);
            uint expectedPackedValue = uint.MaxValue;

            // act
            rgba.FromL16(new L16(ushort.MaxValue));

            // assert
            Assert.Equal(expectedPackedValue, rgba.PackedValue);
        }

        [Fact]
        public void Rgba1010102_FromRgb24()
        {
            // arrange
            var rgba = default(Rgba1010102);
            uint expectedPackedValue = uint.MaxValue;

            // act
            rgba.FromRgb24(new Rgb24(byte.MaxValue, byte.MaxValue, byte.MaxValue));

            // assert
            Assert.Equal(expectedPackedValue, rgba.PackedValue);
        }

        [Fact]
        public void Rgba1010102_FromRgb48()
        {
            // arrange
            var rgba = default(Rgba1010102);
            uint expectedPackedValue = uint.MaxValue;

            // act
            rgba.FromRgb48(new Rgb48(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue));

            // assert
            Assert.Equal(expectedPackedValue, rgba.PackedValue);
        }

        [Fact]
        public void Rgba1010102_FromRgba64()
        {
            // arrange
            var rgba = default(Rgba1010102);
            uint expectedPackedValue = uint.MaxValue;

            // act
            rgba.FromRgba64(new Rgba64(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue));

            // assert
            Assert.Equal(expectedPackedValue, rgba.PackedValue);
        }

        [Fact]
        public void Rgba1010102_Clamping()
        {
            Assert.Equal(Vector4.Zero, new Rgba1010102(Vector4.One * -1234.0f).ToVector4());
            Assert.Equal(Vector4.One, new Rgba1010102(Vector4.One * 1234.0f).ToVector4());
        }

        [Fact]
        public void Rgba1010102_ToRgba32()
        {
            // arrange
            var rgba = new Rgba1010102(0.1f, -0.3f, 0.5f, -0.7f);
            var expected = new Rgba32(25, 0, 128, 0);

            // act
            Rgba32 actual = default;
            rgba.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }
    }
}
