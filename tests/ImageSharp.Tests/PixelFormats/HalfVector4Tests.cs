// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class HalfVector4Tests
    {
        [Fact]
        public void HalfVector4_PackedValue()
        {
            Assert.Equal(0uL, new HalfVector4(Vector4.Zero).PackedValue);
            Assert.Equal(4323521613979991040uL, new HalfVector4(Vector4.One).PackedValue);
            Assert.Equal(13547034390470638592uL, new HalfVector4(-Vector4.One).PackedValue);
            Assert.Equal(15360uL, new HalfVector4(Vector4.UnitX).PackedValue);
            Assert.Equal(1006632960uL, new HalfVector4(Vector4.UnitY).PackedValue);
            Assert.Equal(65970697666560uL, new HalfVector4(Vector4.UnitZ).PackedValue);
            Assert.Equal(4323455642275676160uL, new HalfVector4(Vector4.UnitW).PackedValue);
            Assert.Equal(4035285078724390502uL, new HalfVector4(0.1f, 0.3f, 0.4f, 0.5f).PackedValue);
        }

        [Fact]
        public void HalfVector4_ToVector4()
        {
            Assert.Equal(Vector4.Zero, new HalfVector4(Vector4.Zero).ToVector4());
            Assert.Equal(Vector4.One, new HalfVector4(Vector4.One).ToVector4());
            Assert.Equal(-Vector4.One, new HalfVector4(-Vector4.One).ToVector4());
            Assert.Equal(Vector4.UnitX, new HalfVector4(Vector4.UnitX).ToVector4());
            Assert.Equal(Vector4.UnitY, new HalfVector4(Vector4.UnitY).ToVector4());
            Assert.Equal(Vector4.UnitZ, new HalfVector4(Vector4.UnitZ).ToVector4());
            Assert.Equal(Vector4.UnitW, new HalfVector4(Vector4.UnitW).ToVector4());
        }

        [Fact]
        public void HalfVector4_ToScaledVector4()
        {
            // arrange
            var halfVector4 = new HalfVector4(-Vector4.One);

            // act 
            Vector4 actual = halfVector4.ToScaledVector4();

            // assert
            Assert.Equal(0, actual.X);
            Assert.Equal(0, actual.Y);
            Assert.Equal(0, actual.Z);
            Assert.Equal(0, actual.W);
        }

        [Fact]
        public void HalfVector4_PackFromScaledVector4()
        {
            // arrange
            var halfVector4 = default(HalfVector4);
            Vector4 scaled = new HalfVector4(-Vector4.One).ToScaledVector4();
            ulong expected = 13547034390470638592uL;

            // act 
            halfVector4.PackFromScaledVector4(scaled);
            ulong actual = halfVector4.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector4_ToRgb24()
        {
            // arrange
            var halfVector = new HalfVector4(.25F, .5F, .75F, 1F);
            var actual = default(Rgb24);
            var expected = new Rgb24(64, 128, 191);

            // act
            halfVector.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector4_Rgba32()
        {
            // arrange
            var halfVector = new HalfVector4(.25F, .5F, .75F, 1F);
            var actual = default(Rgba32);
            var expected = new Rgba32(64, 128, 191, 255);

            // act
            halfVector.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector4_ToBgr24()
        {
            // arrange
            var halfVector = new HalfVector4(.25F, .5F, .75F, 1F);
            var actual = default(Bgr24);
            var expected = new Bgr24(64, 128, 191);

            // act
            halfVector.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector4_Bgra32()
        {
            // arrange
            var halfVector = new HalfVector4(.25F, .5F, .75F, 1F);
            var actual = default(Bgra32);
            var expected = new Bgra32(64, 128, 191, 255);

            // act
            halfVector.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector4_Argb32()
        {
            // arrange
            var halfVector = new HalfVector4(.25F, .5F, .75F, 1F);
            var actual = default(Argb32);
            var expected = new Argb32(64, 128, 191, 255);

            // act
            halfVector.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector4_PackFromRgba32_ToRgba32()
        {
            // arrange
            var halVector = default(HalfVector4);
            var actual = default(Rgba32);
            var expected = new Rgba32(64, 128, 191, 255);

            // act
            halVector.PackFromRgba32(expected);
            halVector.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector4_PackFromBgra32_ToBgra32()
        {
            // arrange
            var halVector = default(HalfVector4);
            var actual = default(Bgra32);
            var expected = new Bgra32(64, 128, 191, 255);

            // act
            halVector.PackFromBgra32(expected);
            halVector.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector4_PackFromArgb32_ToArgb32()
        {
            // arrange
            var halVector = default(HalfVector4);
            var actual = default(Argb32);
            var expected = new Argb32(64, 128, 191, 255);

            // act
            halVector.PackFromArgb32(expected);
            halVector.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector4_PackFromRgb48_ToRgb48()
        {
            // arrange
            var input = default(HalfVector4);
            var actual = default(Rgb48);
            var expected = new Rgb48(65535, 0, 65535);

            // act
            input.PackFromRgb48(expected);
            input.ToRgb48(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector4_PackFromRgba64_ToRgba64()
        {
            // arrange
            var input = default(HalfVector4);
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
