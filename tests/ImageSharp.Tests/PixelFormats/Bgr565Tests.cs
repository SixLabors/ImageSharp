// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    [Trait("Category", "PixelFormats")]
    public class Bgr565Tests
    {
        /// <summary>
        /// Tests the equality operators for equality.
        /// </summary>
        [Fact]
        public void AreEqual()
        {
            var color1 = new Bgr565(0.0f, 0.0f, 0.0f);
            var color2 = new Bgr565(new Vector3(0.0f));
            var color3 = new Bgr565(new Vector3(1.0f, 0.0f, 1.0f));
            var color4 = new Bgr565(1.0f, 0.0f, 1.0f);

            Assert.Equal(color1, color2);
            Assert.Equal(color3, color4);
        }

        /// <summary>
        /// Tests the equality operators for inequality.
        /// </summary>
        [Fact]
        public void AreNotEqual()
        {
            var color1 = new Bgr565(0.0f, 0.0f, 0.0f);
            var color2 = new Bgr565(new Vector3(1.0f));
            var color3 = new Bgr565(new Vector3(1.0f, 0.0f, 0.0f));
            var color4 = new Bgr565(1.0f, 1.0f, 0.0f);

            Assert.NotEqual(color1, color2);
            Assert.NotEqual(color3, color4);
        }

        [Fact]
        public void Bgr565_PackedValue()
        {
            Assert.Equal(6160, new Bgr565(0.1F, -0.3F, 0.5F).PackedValue);
            Assert.Equal(0x0, new Bgr565(Vector3.Zero).PackedValue);
            Assert.Equal(0xFFFF, new Bgr565(Vector3.One).PackedValue);

            // Make sure the swizzle is correct.
            Assert.Equal(0xF800, new Bgr565(Vector3.UnitX).PackedValue);
            Assert.Equal(0x07E0, new Bgr565(Vector3.UnitY).PackedValue);
            Assert.Equal(0x001F, new Bgr565(Vector3.UnitZ).PackedValue);
        }

        [Fact]
        public void Bgr565_ToVector3()
        {
            Assert.Equal(Vector3.One, new Bgr565(Vector3.One).ToVector3());
            Assert.Equal(Vector3.Zero, new Bgr565(Vector3.Zero).ToVector3());
            Assert.Equal(Vector3.UnitX, new Bgr565(Vector3.UnitX).ToVector3());
            Assert.Equal(Vector3.UnitY, new Bgr565(Vector3.UnitY).ToVector3());
            Assert.Equal(Vector3.UnitZ, new Bgr565(Vector3.UnitZ).ToVector3());
        }

        [Fact]
        public void Bgr565_ToScaledVector4()
        {
            // arrange
            var bgr = new Bgr565(Vector3.One);

            // act
            Vector4 actual = bgr.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(1, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Bgr565_FromScaledVector4()
        {
            // arrange
            Vector4 scaled = new Bgr565(Vector3.One).ToScaledVector4();
            int expected = 0xFFFF;
            var pixel = default(Bgr565);

            // act
            pixel.FromScaledVector4(scaled);
            ushort actual = pixel.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgr565_FromBgra5551()
        {
            // arrange
            var bgr = default(Bgr565);
            ushort expected = ushort.MaxValue;

            // act
            bgr.FromBgra5551(new Bgra5551(1.0f, 1.0f, 1.0f, 1.0f));

            // assert
            Assert.Equal(expected, bgr.PackedValue);
        }

        [Fact]
        public void Bgr565_FromArgb32()
        {
            // arrange
            var bgr1 = default(Bgr565);
            var bgr2 = default(Bgr565);
            ushort expected1 = ushort.MaxValue;
            ushort expected2 = ushort.MaxValue;

            // act
            bgr1.FromArgb32(new Argb32(1.0f, 1.0f, 1.0f, 1.0f));
            bgr2.FromArgb32(new Argb32(1.0f, 1.0f, 1.0f, 0.0f));

            // assert
            Assert.Equal(expected1, bgr1.PackedValue);
            Assert.Equal(expected2, bgr2.PackedValue);
        }

        [Fact]
        public void Bgr565_FromRgba32()
        {
            // arrange
            var bgr1 = default(Bgr565);
            var bgr2 = default(Bgr565);
            ushort expected1 = ushort.MaxValue;
            ushort expected2 = ushort.MaxValue;

            // act
            bgr1.FromRgba32(new Rgba32(1.0f, 1.0f, 1.0f, 1.0f));
            bgr2.FromRgba32(new Rgba32(1.0f, 1.0f, 1.0f, 0.0f));

            // assert
            Assert.Equal(expected1, bgr1.PackedValue);
            Assert.Equal(expected2, bgr2.PackedValue);
        }

        [Fact]
        public void Bgr565_ToRgba32()
        {
            // arrange
            var bgra = new Bgr565(Vector3.One);
            var expected = new Rgba32(Vector4.One);
            var actual = default(Rgba32);

            // act
            bgra.ToRgba32(ref actual);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra565_FromRgb48()
        {
            // arrange
            var bgr = default(Bgr565);
            ushort expectedPackedValue = ushort.MaxValue;

            // act
            bgr.FromRgb48(new Rgb48(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue));

            // assert
            Assert.Equal(expectedPackedValue, bgr.PackedValue);
        }

        [Fact]
        public void Bgra565_FromRgba64()
        {
            // arrange
            var bgr = default(Bgr565);
            ushort expectedPackedValue = ushort.MaxValue;

            // act
            bgr.FromRgba64(new Rgba64(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue));

            // assert
            Assert.Equal(expectedPackedValue, bgr.PackedValue);
        }

        [Fact]
        public void Bgr565_FromBgr24()
        {
            // arrange
            var bgr = default(Bgr565);
            ushort expected = ushort.MaxValue;

            // act
            bgr.FromBgr24(new Bgr24(byte.MaxValue, byte.MaxValue, byte.MaxValue));

            // assert
            Assert.Equal(expected, bgr.PackedValue);
        }

        [Fact]
        public void Bgr565_FromRgb24()
        {
            // arrange
            var bgr = default(Bgr565);
            ushort expected = ushort.MaxValue;

            // act
            bgr.FromRgb24(new Rgb24(byte.MaxValue, byte.MaxValue, byte.MaxValue));

            // assert
            Assert.Equal(expected, bgr.PackedValue);
        }

        [Fact]
        public void Bgr565_FromGrey8()
        {
            // arrange
            var bgr = default(Bgr565);
            ushort expected = ushort.MaxValue;

            // act
            bgr.FromL8(new L8(byte.MaxValue));

            // assert
            Assert.Equal(expected, bgr.PackedValue);
        }

        [Fact]
        public void Bgr565_FromGrey16()
        {
            // arrange
            var bgr = default(Bgr565);
            ushort expected = ushort.MaxValue;

            // act
            bgr.FromL16(new L16(ushort.MaxValue));

            // assert
            Assert.Equal(expected, bgr.PackedValue);
        }

        [Fact]
        public void Bgr565_Clamping()
        {
            Assert.Equal(Vector3.Zero, new Bgr565(Vector3.One * -1234F).ToVector3());
            Assert.Equal(Vector3.One, new Bgr565(Vector3.One * 1234F).ToVector3());
        }
    }
}
