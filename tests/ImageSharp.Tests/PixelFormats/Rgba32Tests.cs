// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    /// <summary>
    /// Tests the <see cref="Rgba32"/> struct.
    /// </summary>
    public class Rgba32Tests
    {
        /// <summary>
        /// Tests the equality operators for equality.
        /// </summary>
        [Fact]
        public void AreEqual()
        {
            Rgba32 color1 = new Rgba32(0, 0, 0);
            Rgba32 color2 = new Rgba32(0, 0, 0, 1F);
            Rgba32 color3 = Rgba32.FromHex("#000");
            Rgba32 color4 = Rgba32.FromHex("#000F");
            Rgba32 color5 = Rgba32.FromHex("#000000");
            Rgba32 color6 = Rgba32.FromHex("#000000FF");

            Assert.Equal(color1, color2);
            Assert.Equal(color1, color3);
            Assert.Equal(color1, color4);
            Assert.Equal(color1, color5);
            Assert.Equal(color1, color6);
        }

        /// <summary>
        /// Tests the equality operators for inequality.
        /// </summary>
        [Fact]
        public void AreNotEqual()
        {
            Rgba32 color1 = new Rgba32(255, 0, 0, 255);
            Rgba32 color2 = new Rgba32(0, 0, 0, 255);
            Rgba32 color3 = Rgba32.FromHex("#000");
            Rgba32 color4 = Rgba32.FromHex("#000000");
            Rgba32 color5 = Rgba32.FromHex("#FF000000");

            Assert.NotEqual(color1, color2);
            Assert.NotEqual(color1, color3);
            Assert.NotEqual(color1, color4);
            Assert.NotEqual(color1, color5);
        }

        /// <summary>
        /// Tests whether the color constructor correctly assign properties.
        /// </summary>
        [Fact]
        public void ConstructorAssignsProperties()
        {
            Rgba32 color1 = new Rgba32(1, .1f, .133f, .864f);
            Assert.Equal(255, color1.R);
            Assert.Equal((byte)Math.Round(.1f * 255), color1.G);
            Assert.Equal((byte)Math.Round(.133f * 255), color1.B);
            Assert.Equal((byte)Math.Round(.864f * 255), color1.A);

            Rgba32 color2 = new Rgba32(1, .1f, .133f);
            Assert.Equal(255, color2.R);
            Assert.Equal(Math.Round(.1f * 255), color2.G);
            Assert.Equal(Math.Round(.133f * 255), color2.B);
            Assert.Equal(255, color2.A);

            Rgba32 color4 = new Rgba32(new Vector3(1, .1f, .133f));
            Assert.Equal(255, color4.R);
            Assert.Equal(Math.Round(.1f * 255), color4.G);
            Assert.Equal(Math.Round(.133f * 255), color4.B);
            Assert.Equal(255, color4.A);

            Rgba32 color5 = new Rgba32(new Vector4(1, .1f, .133f, .5f));
            Assert.Equal(255, color5.R);
            Assert.Equal(Math.Round(.1f * 255), color5.G);
            Assert.Equal(Math.Round(.133f * 255), color5.B);
            Assert.Equal(Math.Round(.5f * 255), color5.A);
        }

        /// <summary>
        /// Tests whether FromHex and ToHex work correctly.
        /// </summary>
        [Fact]
        public void FromAndToHex()
        {
            // 8 digit hex matches css4 spec. RRGGBBAA
            var color = Rgba32.FromHex("#AABBCCDD"); // 170, 187, 204, 221
            Assert.Equal(170, color.R);
            Assert.Equal(187, color.G);
            Assert.Equal(204, color.B);
            Assert.Equal(221, color.A);

            Assert.Equal("AABBCCDD", color.ToHex());

            color.R = 0;

            Assert.Equal("00BBCCDD", color.ToHex());

            color.A = 255;

            Assert.Equal("00BBCCFF", color.ToHex());
        }

        /// <summary>
        /// Tests that the individual byte elements are layed out in RGBA order.
        /// </summary>
        [Fact]
        public unsafe void ByteLayout()
        {
            Rgba32 color = new Rgba32(1, 2, 3, 4);
            byte* colorBase = (byte*)&color;
            Assert.Equal(1, colorBase[0]);
            Assert.Equal(2, colorBase[1]);
            Assert.Equal(3, colorBase[2]);
            Assert.Equal(4, colorBase[3]);

            Assert.Equal(4, sizeof(Rgba32));
        }

        [Fact]
        public void Rgba32_PackedValues()
        {
            Assert.Equal(0x80001Au, new Rgba32(+0.1f, -0.3f, +0.5f, -0.7f).PackedValue);
            // Test the limits.
            Assert.Equal((uint)0x0, new Rgba32(Vector4.Zero).PackedValue);
            Assert.Equal(0xFFFFFFFF, new Rgba32(Vector4.One).PackedValue);
        }

        [Fact]
        public void Rgba32_ToVector4()
        {
            Assert.Equal(Vector4.One, new Rgba32(Vector4.One).ToVector4());
            Assert.Equal(Vector4.Zero, new Rgba32(Vector4.Zero).ToVector4());
            Assert.Equal(Vector4.UnitX, new Rgba32(Vector4.UnitX).ToVector4());
            Assert.Equal(Vector4.UnitY, new Rgba32(Vector4.UnitY).ToVector4());
            Assert.Equal(Vector4.UnitZ, new Rgba32(Vector4.UnitZ).ToVector4());
            Assert.Equal(Vector4.UnitW, new Rgba32(Vector4.UnitW).ToVector4());
        }

        [Fact]
        public void Rgba32_ToScaledVector4()
        {
            // arrange
            var rgba = new Rgba32(Vector4.One);

            // act
            Vector4 actual = rgba.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(1, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Rgba32_PackFromScaledVector4()
        {
            // arrange
            var rgba = new Rgba32(Vector4.One);
            var actual = default(Rgba32);
            uint expected = 0xFFFFFFFF;

            // act
            Vector4 scaled = rgba.ToScaledVector4();
            actual.PackFromScaledVector4(scaled);

            // assert
            Assert.Equal(expected, actual.PackedValue);
        }

        [Fact]
        public void Rgba32_Clamping()
        {
            Assert.Equal(Vector4.Zero, new Rgba32(Vector4.One * -1234.0f).ToVector4());
            Assert.Equal(Vector4.One, new Rgba32(Vector4.One * +1234.0f).ToVector4());
        }

        [Fact]
        public void Rgba32_ToRgb24()
        {
            // arrange
            var rgba = new Rgba32(+0.1f, -0.3f, +0.5f, -0.7f);
            var actual = default(Rgb24);
            var expected = new Rgb24(0x1a, 0, 0x80);

            // act
            rgba.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected.R, actual.R);
            Assert.Equal(expected.G, actual.G);
            Assert.Equal(expected.B, actual.B);
        }

        [Fact]
        public void Rgba32_ToRgba32()
        {
            // arrange
            var rgba = new Rgba32(+0.1f, -0.3f, +0.5f, -0.7f);
            var actual = default(Rgba32);
            var expected = new Rgba32(0x1a, 0, 0x80, 0);

            // act
            rgba.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba32_ToBgr24()
        {
            // arrange
            var rgba = new Rgba32(+0.1f, -0.3f, +0.5f, -0.7f);
            var actual = default(Bgr24);
            var expected = new Bgr24(0x1a, 0, 0x80);

            // act
            rgba.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba32_ToBgra32()
        {
            // arrange
            var rgba = new Rgba32(+0.1f, -0.3f, +0.5f, -0.7f);
            var actual = default(Bgra32);
            var expected = new Bgra32(0x1a, 0, 0x80, 0);

            // act
            rgba.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba32_ToArgb32()
        {
            // arrange
            var rgba = new Rgba32(+0.1f, -0.3f, +0.5f, -0.7f);
            var actual = default(Argb32);
            var expected = new Argb32(0x1a, 0, 0x80, 0);

            // act
            rgba.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba32_PackFromRgba32_ToRgba32()
        {
            // arrange
            var rgba = default(Rgba32);
            var actual = default(Rgba32);
            var expected = new Rgba32(0x1a, 0, 0x80, 0);

            // act 
            rgba.PackFromRgba32(expected);
            rgba.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba32_PackFromBgra32_ToRgba32()
        {
            // arrange
            var rgba = default(Rgba32);
            var actual = default(Bgra32);
            var expected = new Bgra32(0x1a, 0, 0x80, 0);

            // act 
            rgba.PackFromBgra32(expected);
            rgba.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba32_PackFromArgb32_ToArgb32()
        {
            // arrange
            var rgba = default(Rgba32);
            var actual = default(Argb32);
            var expected = new Argb32(0x1a, 0, 0x80, 0);

            // act 
            rgba.PackFromArgb32(expected);
            rgba.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba32_PackFromRgb48_ToRgb48()
        {
            // arrange
            var input = default(Rgba32);
            var actual = default(Rgb48);
            var expected = new Rgb48(65535, 0, 65535);

            // act
            input.PackFromRgb48(expected);
            input.ToRgb48(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba32_PackFromRgba64_ToRgba64()
        {
            // arrange
            var input = default(Rgba32);
            var actual = default(Rgba64);
            var expected = new Rgba64(65535, 0, 65535, 0);

            // act
            input.PackFromRgba64(expected);
            input.ToRgba64(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }
    }
}