// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    [Trait("Category", "PixelFormats")]
    public class Byte4Tests
    {
        /// <summary>
        /// Tests the equality operators for equality.
        /// </summary>
        [Fact]
        public void AreEqual()
        {
            var color1 = new Byte4(0.0f, 0.0f, 0.0f, 0.0f);
            var color2 = new Byte4(new Vector4(0.0f));
            var color3 = new Byte4(new Vector4(1.0f, 0.0f, 1.0f, 1.0f));
            var color4 = new Byte4(1.0f, 0.0f, 1.0f, 1.0f);

            Assert.Equal(color1, color2);
            Assert.Equal(color3, color4);
        }

        /// <summary>
        /// Tests the equality operators for inequality.
        /// </summary>
        [Fact]
        public void AreNotEqual()
        {
            var color1 = new Byte4(0.0f, 0.0f, 0.0f, 0.0f);
            var color2 = new Byte4(new Vector4(1.0f));
            var color3 = new Byte4(new Vector4(1.0f, 0.0f, 0.0f, 1.0f));
            var color4 = new Byte4(1.0f, 1.0f, 0.0f, 1.0f);

            Assert.NotEqual(color1, color2);
            Assert.NotEqual(color3, color4);
        }

        [Fact]
        public void Byte4_PackedValue()
        {
            Assert.Equal(128U, new Byte4(127.5f, -12.3f, 0.5f, -0.7f).PackedValue);
            Assert.Equal(0x1a7b362dU, new Byte4(0x2d, 0x36, 0x7b, 0x1a).PackedValue);
            Assert.Equal(0x0U, new Byte4(Vector4.Zero).PackedValue);
            Assert.Equal(0xFFFFFFFF, new Byte4(Vector4.One * 255).PackedValue);
        }

        [Fact]
        public void Byte4_ToVector4()
        {
            Assert.Equal(Vector4.One * 255, new Byte4(Vector4.One * 255).ToVector4());
            Assert.Equal(Vector4.Zero, new Byte4(Vector4.Zero).ToVector4());
            Assert.Equal(Vector4.UnitX * 255, new Byte4(Vector4.UnitX * 255).ToVector4());
            Assert.Equal(Vector4.UnitY * 255, new Byte4(Vector4.UnitY * 255).ToVector4());
            Assert.Equal(Vector4.UnitZ * 255, new Byte4(Vector4.UnitZ * 255).ToVector4());
            Assert.Equal(Vector4.UnitW * 255, new Byte4(Vector4.UnitW * 255).ToVector4());
        }

        [Fact]
        public void Byte4_ToScaledVector4()
        {
            // arrange
            var byte4 = new Byte4(Vector4.One * 255);

            // act
            Vector4 actual = byte4.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(1, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Byte4_ToRgba32()
        {
            // arrange
            var byte4 = new Byte4(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
            var expected = new Rgba32(Vector4.One);
            var actual = default(Rgba32);

            // act
            byte4.ToRgba32(ref actual);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Byte4_FromScaledVector4()
        {
            // arrange
            Vector4 scaled = new Byte4(Vector4.One * 255).ToScaledVector4();
            var pixel = default(Byte4);
            uint expected = 0xFFFFFFFF;

            // act
            pixel.FromScaledVector4(scaled);
            uint actual = pixel.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Byte4_FromArgb32()
        {
            // arrange
            var byte4 = default(Byte4);
            uint expectedPackedValue = uint.MaxValue;

            // act
            byte4.FromArgb32(new Argb32(255, 255, 255, 255));

            // assert
            Assert.Equal(expectedPackedValue, byte4.PackedValue);
        }

        [Fact]
        public void Byte4_FromBgr24()
        {
            // arrange
            var byte4 = default(Byte4);
            uint expectedPackedValue = uint.MaxValue;

            // act
            byte4.FromBgr24(new Bgr24(byte.MaxValue, byte.MaxValue, byte.MaxValue));

            // assert
            Assert.Equal(expectedPackedValue, byte4.PackedValue);
        }

        [Fact]
        public void Byte4_FromGrey8()
        {
            // arrange
            var byte4 = default(Byte4);
            uint expectedPackedValue = uint.MaxValue;

            // act
            byte4.FromL8(new L8(byte.MaxValue));

            // assert
            Assert.Equal(expectedPackedValue, byte4.PackedValue);
        }

        [Fact]
        public void Byte4_FromGrey16()
        {
            // arrange
            var byte4 = default(Byte4);
            uint expectedPackedValue = uint.MaxValue;

            // act
            byte4.FromL16(new L16(ushort.MaxValue));

            // assert
            Assert.Equal(expectedPackedValue, byte4.PackedValue);
        }

        [Fact]
        public void Byte4_FromRgb24()
        {
            // arrange
            var byte4 = default(Byte4);
            uint expectedPackedValue = uint.MaxValue;

            // act
            byte4.FromRgb24(new Rgb24(byte.MaxValue, byte.MaxValue, byte.MaxValue));

            // assert
            Assert.Equal(expectedPackedValue, byte4.PackedValue);
        }

        [Fact]
        public void Byte4_FromBgra5551()
        {
            // arrange
            var byte4 = default(Byte4);
            uint expected = 0xFFFFFFFF;

            // act
            byte4.FromBgra5551(new Bgra5551(1.0f, 1.0f, 1.0f, 1.0f));

            // assert
            Assert.Equal(expected, byte4.PackedValue);
        }

        [Fact]
        public void Byte4_FromRgba32()
        {
            // arrange
            var byte4 = default(Byte4);
            uint expectedPackedValue1 = uint.MaxValue;

            // act
            byte4.FromRgba32(new Rgba32(255, 255, 255, 255));

            // assert
            Assert.Equal(expectedPackedValue1, byte4.PackedValue);
        }

        [Fact]
        public void Byte4_FromRgb48()
        {
            // arrange
            var byte4 = default(Byte4);
            uint expectedPackedValue = uint.MaxValue;

            // act
            byte4.FromRgb48(new Rgb48(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue));

            // assert
            Assert.Equal(expectedPackedValue, byte4.PackedValue);
        }

        [Fact]
        public void Byte4_FromRgba64()
        {
            // arrange
            var byte4 = default(Byte4);
            uint expectedPackedValue = uint.MaxValue;

            // act
            byte4.FromRgba64(new Rgba64(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue));

            // assert
            Assert.Equal(expectedPackedValue, byte4.PackedValue);
        }

        [Fact]
        public void Byte4_Clamping()
        {
            Assert.Equal(Vector4.Zero, new Byte4(Vector4.One * -1234.0f).ToVector4());
            Assert.Equal(Vector4.One * 255, new Byte4(Vector4.One * 1234.0f).ToVector4());
        }
    }
}
