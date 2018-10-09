// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class Gray16Tests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(65535)]
        [InlineData(32767)]
        [InlineData(42)]
        public void Gray16_PackedValue_EqualsInput(ushort input)
        {
            Assert.Equal(input, new Gray16(input).PackedValue);
        }

        //[Theory]
        //[InlineData(0)]
        //[InlineData(65535)]
        //[InlineData(32767)]
        //public void Gray16_ToVector4(ushort input)
        //{
        //    // arrange
        //    var gray = new Gray16(input);

        //    // act
        //    var actual = gray.ToVector4();

        //    // assert
        //    Assert.Equal(input, actual.X);
        //    Assert.Equal(input, actual.Y);
        //    Assert.Equal(input, actual.Z);
        //    Assert.Equal(1, actual.W);
        //}

        //[Theory]
        //[InlineData(0)]
        //[InlineData(65535)]
        //[InlineData(32767)]
        //public void Gray16_ToScaledVector4(ushort input)
        //{
        //    // arrange
        //    var gray = new Gray16(input);

        //    // act
        //    var actual = gray.ToScaledVector4();

        //    // assert
        //    float scaledInput = input / 65535f;
        //    Assert.Equal(scaledInput, actual.X);
        //    Assert.Equal(scaledInput, actual.Y);
        //    Assert.Equal(scaledInput, actual.Z);
        //    Assert.Equal(1, actual.W);
        //}

        //[Fact]
        //public void Gray16_PackFromScaledVector4()
        //{
        //    // arrange
        //    Gray16 gray = default;
        //    int expected = 32767;
        //    Vector4 scaled = new Gray16((ushort)expected).ToScaledVector4();

        //    // act
        //    gray.PackFromScaledVector4(scaled);
        //    ushort actual = gray.PackedValue;

        //    // assert
        //    Assert.Equal(expected, actual);
        //}

        //[Fact]
        //public void Gray16_PackFromScaledVector4_ToRgb24()
        //{
        //    // arrange
        //    Rgb24 actual = default;
        //    Gray16 gray = default;
        //    var expected = new Rgb24(128, 128, 128);
        //    Vector4 scaled = new Gray16(32768).ToScaledVector4();

        //    // act
        //    gray.PackFromScaledVector4(scaled);
        //    gray.ToRgb24(ref actual);

        //    // assert
        //    Assert.Equal(expected, actual);
        //}

        //[Fact]
        //public void Gray16_PackFromScaledVector4_ToRgba32()
        //{
        //    // arrange
        //    Rgba32 actual = default;
        //    Gray16 gray = default;
        //    var expected = new Rgba32(128, 128, 128, 255);
        //    Vector4 scaled = new Gray16(32768).ToScaledVector4();

        //    // act
        //    gray.PackFromScaledVector4(scaled);
        //    gray.ToRgba32(ref actual);

        //    // assert
        //    Assert.Equal(expected, actual);
        //}

        //[Fact]
        //public void Gray16_PackFromScaledVector4_ToBgr24()
        //{
        //    // arrange
        //    Bgr24 actual = default;
        //    Gray16 gray = default;
        //    var expected = new Bgr24(128, 128, 128);
        //    Vector4 scaled = new Gray16(32768).ToScaledVector4();

        //    // act
        //    gray.PackFromScaledVector4(scaled);
        //    gray.ToBgr24(ref actual);

        //    // assert
        //    Assert.Equal(expected, actual);
        //}

        //[Fact]
        //public void Gray16_PackFromScaledVector4_ToBgra32()
        //{
        //    // arrange
        //    Bgra32 actual = default;
        //    Gray16 gray = default;
        //    var expected = new Bgra32(128,128,128);
        //    Vector4 scaled = new Gray16(32768).ToScaledVector4();

        //    // act
        //    gray.PackFromScaledVector4(scaled);
        //    gray.ToBgra32(ref actual);

        //    // assert
        //    Assert.Equal(expected, actual);
        //}

        //[Fact]
        //public void Gray16_PackFromScaledVector4_ToArgb32()
        //{
        //    // arrange
        //    Gray16 gray = default;
        //    Argb32 actual = default;
        //    var expected = new Argb32(128, 128, 128);
        //    Vector4 scaled = new Gray16(32768).ToScaledVector4();

        //    // act
        //    gray.PackFromScaledVector4(scaled);
        //    gray.ToArgb32(ref actual);

        //    // assert
        //    Assert.Equal(expected, actual);
        //}

        //[Fact]
        //public void Gray16_PackFromScaledVector4_ToRgba64()
        //{
        //    // arrange
        //    Gray16 gray = default;
        //    Rgba64 actual = default;
        //    var expected = new Rgba64(65535, 65535, 65535, 65535);
        //    Vector4 scaled = new Gray16(65535).ToScaledVector4();

        //    // act
        //    gray.PackFromScaledVector4(scaled);
        //    gray.ToRgba64(ref actual);

        //    // assert
        //    Assert.Equal(expected, actual);
        //}

        //[Fact]
        //public void Gray16_PackFromRgb48_ToRgb48()
        //{
        //    // arrange
        //    var gray = default(Gray16);
        //    var actual = default(Rgb48);
        //    var expected = new Rgb48(0, 0, 0);

        //    // act
        //    gray.PackFromRgb48(expected);
        //    gray.ToRgb48(ref actual);

        //    // assert
        //    Assert.Equal(expected, actual);
        //}

        //[Fact]
        //public void Gray16_PackFromRgba64_ToRgba64()
        //{
        //    // arrange
        //    var gray = default(Gray16);
        //    var actual = default(Rgba64);
        //    var expected = new Rgba64(0, 0, 0, 65535);

        //    // act
        //    gray.PackFromRgba64(expected);
        //    gray.ToRgba64(ref actual);

        //    // assert
        //    Assert.Equal(expected, actual);
        //}
    }
}
