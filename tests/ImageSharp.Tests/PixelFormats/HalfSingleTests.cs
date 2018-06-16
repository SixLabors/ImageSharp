// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class HalfSingleTests
    {
        [Fact]
        public void HalfSingle_PackedValue()
        {
            Assert.Equal(11878, new HalfSingle(0.1F).PackedValue);
            Assert.Equal(46285, new HalfSingle(-0.3F).PackedValue);

            // Test limits
            Assert.Equal(15360, new HalfSingle(1F).PackedValue);
            Assert.Equal(0, new HalfSingle(0F).PackedValue);
            Assert.Equal(48128, new HalfSingle(-1F).PackedValue);
        }

        [Fact]
        public void HalfSingle_ToVector4()
        {
            // arrange
            var halfSingle = new HalfSingle(0.5f);
            var expected = new Vector4(0.5f, 0, 0, 1);

            // act
            var actual = halfSingle.ToVector4();

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfSingle_ToScaledVector4()
        {
            // arrange
            var halfSingle = new HalfSingle(-1F);

            // act 
            Vector4 actual = halfSingle.ToScaledVector4();

            // assert
            Assert.Equal(0, actual.X);
            Assert.Equal(0, actual.Y);
            Assert.Equal(0, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void HalfSingle_PackFromScaledVector4()
        {
            // arrange 
            Vector4 scaled = new HalfSingle(-1F).ToScaledVector4();
            int expected = 48128;
            var halfSingle = default(HalfSingle);

            // act
            halfSingle.PackFromScaledVector4(scaled);
            ushort actual = halfSingle.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfSingle_ToRgb24()
        {
            // arrange
            var halfVector = new HalfSingle(.5F);
            var actual = default(Rgb24);
            var expected = new Rgb24(128, 0, 0);

            // act
            halfVector.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfSingle_Rgba32()
        {
            // arrange
            var halfVector = new HalfSingle(.5F);
            var actual = default(Rgba32);
            var expected = new Rgba32(128, 0, 0, 255);

            // act
            halfVector.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfSingle_ToBgr24()
        {
            // arrange
            var halfVector = new HalfSingle(.5F);
            var actual = default(Bgr24);
            var expected = new Bgr24(128, 0, 0);

            // act
            halfVector.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfSingle_Bgra32()
        {
            // arrange
            var halfVector = new HalfSingle(.5F);
            var actual = default(Bgra32);
            var expected = new Bgra32(128, 0, 0, 255);

            // act
            halfVector.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfSingle_Argb32()
        {
            // arrange
            var halfVector = new HalfSingle(.5F);
            var actual = default(Argb32);
            var expected = new Argb32(128, 0, 0, 255);

            // act
            halfVector.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfSingle_PackFromRgb48_ToRgb48()
        {
            // arrange
            var input = default(HalfSingle);
            var actual = default(Rgb48);
            var expected = new Rgb48(65535, 0, 0);

            // act
            input.PackFromRgb48(expected);
            input.ToRgb48(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfSingle_PackFromRgba64_ToRgba64()
        {
            // arrange
            var input = default(HalfSingle);
            var actual = default(Rgba64);
            var expected = new Rgba64(65535, 0, 0, 65535);

            // act
            input.PackFromRgba64(expected);
            input.ToRgba64(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }
    }
}
