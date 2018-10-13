// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class Rgb48Tests
    {
        [Fact]
        public void Rgb48_Values()
        {
            var rgb = new Rgba64(5243, 9830, 19660, 29491);

            Assert.Equal(5243, rgb.R);
            Assert.Equal(9830, rgb.G);
            Assert.Equal(19660, rgb.B);
            Assert.Equal(29491, rgb.A);
        }

        [Fact]
        public void Rgb48_ToVector4()
            => Assert.Equal(Vector4.One, new Rgb48(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue).ToVector4());

        [Fact]
        public void Rgb48_ToScaledVector4()
            => Assert.Equal(Vector4.One, new Rgb48(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue).ToVector4());

        [Fact]
        public void Rgb48_PackFromScaledVector4()
        {
            // arrange
            var pixel = default(Rgb48);
            var short3 = new Rgb48(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue);
            var expected = new Rgb48(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue);

            // act 
            Vector4 scaled = short3.ToScaledVector4();
            pixel.PackFromScaledVector4(scaled);

            // assert
            Assert.Equal(expected, pixel);
        }

        [Fact]
        public void Rgb48_ToRgba32()
        {
            // arrange
            var rgba48 = new Rgb48(5140, 9766, 19532);
            var expected = new Rgba32(20, 38, 76, 255);

            // act
            var actual = rgba48.ToRgba32();

            // assert
            Assert.Equal(expected, actual);
        }
    }
}