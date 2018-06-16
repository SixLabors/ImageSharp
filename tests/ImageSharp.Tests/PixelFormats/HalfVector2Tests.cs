// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class HalfVector2Tests
    {
        [Fact]
        public void HalfVector2_PackedValue()
        {
            Assert.Equal(0u, new HalfVector2(Vector2.Zero).PackedValue);
            Assert.Equal(1006648320u, new HalfVector2(Vector2.One).PackedValue);
            Assert.Equal(3033345638u, new HalfVector2(0.1f, -0.3f).PackedValue);
        }

        [Fact]
        public void HalfVector2_ToVector2()
        {
            Assert.Equal(Vector2.Zero, new HalfVector2(Vector2.Zero).ToVector2());
            Assert.Equal(Vector2.One, new HalfVector2(Vector2.One).ToVector2());
            Assert.Equal(Vector2.UnitX, new HalfVector2(Vector2.UnitX).ToVector2());
            Assert.Equal(Vector2.UnitY, new HalfVector2(Vector2.UnitY).ToVector2());
        }

        [Fact]
        public void HalfVector2_ToScaledVector4()
        {
            // arrange
            var halfVector = new HalfVector2(Vector2.One);

            // act
            Vector4 actual = halfVector.ToScaledVector4();

            // assert
            Assert.Equal(1F, actual.X);
            Assert.Equal(1F, actual.Y);
            Assert.Equal(0, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void HalfVector2_PackFromScaledVector4()
        {
            // arrange
            Vector4 scaled = new HalfVector2(Vector2.One).ToScaledVector4();
            uint expected = 1006648320u;
            var halfVector = default(HalfVector2);

            // act
            halfVector.PackFromScaledVector4(scaled);
            uint actual = halfVector.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector2_ToVector4()
        {
            // arrange
            var halfVector = new HalfVector2(.5F, .25F);
            var expected = new Vector4(0.5f, .25F, 0, 1);

            // act
            var actual = halfVector.ToVector4();

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector2_ToRgb24()
        {
            // arrange
            var halfVector = new HalfVector2(.5F, .25F);
            var actual = default(Rgb24);
            var expected = new Rgb24(128, 64, 0);

            // act
            halfVector.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector2_Rgba32()
        {
            // arrange
            var halfVector = new HalfVector2(.5F, .25F);
            var actual = default(Rgba32);
            var expected = new Rgba32(128, 64, 0, 255);

            // act
            halfVector.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector2_ToBgr24()
        {
            // arrange
            var halfVector = new HalfVector2(.5F, .25F);
            var actual = default(Bgr24);
            var expected = new Bgr24(128, 64, 0);

            // act
            halfVector.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector2_Bgra32()
        {
            // arrange
            var halfVector = new HalfVector2(.5F, .25F);
            var actual = default(Bgra32);
            var expected = new Bgra32(128, 64, 0, 255);

            // act
            halfVector.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector2_Argb32()
        {
            // arrange
            var halfVector = new HalfVector2(.5F, .25F);
            var actual = default(Argb32);
            var expected = new Argb32(128, 64, 0, 255);

            // act
            halfVector.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector2_PackFromRgb48_ToRgb48()
        {
            // arrange
            var input = default(HalfVector2);
            var actual = default(Rgb48);
            var expected = new Rgb48(65535, 65535, 0);

            // act
            input.PackFromRgb48(expected);
            input.ToRgb48(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector2_PackFromRgba64_ToRgba64()
        {
            // arrange
            var input = default(HalfVector2);
            var actual = default(Rgba64);
            var expected = new Rgba64(65535, 65535, 0, 65535);

            // act
            input.PackFromRgba64(expected);
            input.ToRgba64(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }
    }
}
