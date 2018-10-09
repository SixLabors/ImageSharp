// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class Rgba64Tests
    {
        [Fact]
        public void Rgba64_PackedValues()
        {
            Assert.Equal((ulong)0x73334CCC2666147B, new Rgba64(5243, 9830, 19660, 29491).PackedValue);

            // Test the limits.
            Assert.Equal((ulong)0x0, new Rgba64(0, 0, 0, 0).PackedValue);
            Assert.Equal(0xFFFFFFFFFFFFFFFF, new Rgba64(
                ushort.MaxValue,
                ushort.MaxValue,
                ushort.MaxValue,
                ushort.MaxValue).PackedValue);

            // Test data ordering
            Assert.Equal(0xC7AD8F5C570A1EB8, new Rgba64(0x1EB8, 0x570A, 0x8F5C, 0xC7AD).PackedValue);
        }

        [Fact]
        public void Rgba64_ToVector4()
        {
            Assert.Equal(Vector4.Zero, new Rgba64(0, 0, 0, 0).ToVector4());
            Assert.Equal(Vector4.One, new Rgba64(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue).ToVector4());
        }

        [Fact]
        public void Rgba64_ToScaledVector4()
        {
            // arrange
            var short2 = new Rgba64(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue);

            // act
            Vector4 actual = short2.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(1, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Rgba64_PackFromScaledVector4()
        {
            // arrange
            var pixel = default(Rgba64);
            var short4 = new Rgba64(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue);
            const ulong expected = 0xFFFFFFFFFFFFFFFF;

            // act 
            Vector4 scaled = short4.ToScaledVector4();
            pixel.PackFromScaledVector4(scaled);
            ulong actual = pixel.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba64_Clamping()
        {
            var zero = default(Rgba64);
            var one = default(Rgba64);
            zero.PackFromVector4(Vector4.One * -1234.0f);
            one.PackFromVector4(Vector4.One * 1234.0f);
            Assert.Equal(Vector4.Zero, zero.ToVector4());
            Assert.Equal(Vector4.One, one.ToVector4());
        }

        //[Fact]
        //public void Rgba64_ToRgba32()
        //{
        //    // arrange
        //    var rgba64 = new Rgba64(
        //        (ushort)(0.08f * ushort.MaxValue),
        //        (ushort)(0.15f * ushort.MaxValue),
        //        (ushort)(0.30f * ushort.MaxValue),
        //        (ushort)(0.45f * ushort.MaxValue));
        //    var actual = default(Rgba32);
        //    var expected = new Rgba32(20, 38, 76, 115);

        //    // act
        //    actual = rgba64.ToRgba32();

        //    // assert
        //    Assert.Equal(expected, actual);
        //}

        //[Fact]
        //public void Rgba64_PackFromRgba32_ToRgba32()
        //{
        //    // arrange
        //    var rgba64 = default(Rgba64);
        //    var actual = default(Rgba32);
        //    var expected = new Rgba32(20, 38, 76, 115);

        //    // act 
        //    rgba64.PackFromRgba32(expected);
        //    actual = rgba64.ToRgba32();

        //    // assert
        //    Assert.Equal(expected, actual);
        //}

        //[Fact]
        //public void Rgb48_PackFromRgb48()
        //{
        //    // arrange
        //    var input = default(Rgba64);
        //    var actual = default(Rgb48);
        //    var expected = new Rgb48(65535, 0, 65535);

        //    // act
        //    input.PackFromRgb48(expected);

        //    // assert
        //    Assert.Equal(expected, actual);
        //}

        //[Fact]
        //public void Rgba64_PackFromRgba64()
        //{
        //    // arrange
        //    var input = default(Rgba64);
        //    var actual = default(Rgba64);
        //    var expected = new Rgba64(65535, 0, 65535, 0);

        //    // act
        //    input.PackFromRgba64(expected);
        //    actual.PackFromScaledVector4(input.ToScaledVector4());

        //    // assert
        //    Assert.Equal(expected, actual);
        //}
    }
}
