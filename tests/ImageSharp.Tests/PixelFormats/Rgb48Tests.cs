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

            rgb = new Rgba64(5243 / 65535F, 9830 / 65535F, 19660 / 65535F, 29491 / 65535F);

            Assert.Equal(5243, rgb.R);
            Assert.Equal(9830, rgb.G);
            Assert.Equal(19660, rgb.B);
            Assert.Equal(29491, rgb.A);
        }

        [Fact]
        public void Rgb48_ToVector4()
        {
            Assert.Equal(new Vector4(0, 0, 0, 1), new Rgb48(Vector3.Zero).ToVector4());
            Assert.Equal(Vector4.One, new Rgb48(Vector3.One).ToVector4());
        }

        [Fact]
        public void Rgb48_ToScaledVector4()
        {
            // arrange
            var short2 = new Rgb48(Vector3.One);

            // act
            Vector4 actual = short2.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(1, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Rgb48_PackFromScaledVector4()
        {
            // arrange
            var pixel = default(Rgb48);
            var short3 = new Rgb48(Vector3.One);
            var expected = new Rgb48(Vector3.One);

            // act 
            Vector4 scaled = short3.ToScaledVector4();
            pixel.PackFromScaledVector4(scaled);

            // assert
            Assert.Equal(expected, pixel);
        }

        [Fact]
        public void Rgb48_Clamping()
        {
            Assert.Equal(new Vector4(0, 0, 0, 1), new Rgb48(Vector3.One * -1234.0f).ToVector4());
            Assert.Equal(Vector4.One, new Rgb48(Vector3.One * 1234.0f).ToVector4());
        }

        [Fact]
        public void Rgb48_ToRgb24()
        {
            // arrange
            var rgba48 = new Rgb48(0.08f, 0.15f, 0.30f);
            var actual = default(Rgb24);
            var expected = new Rgb24(20, 38, 76);

            // act
            rgba48.ToRgb24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgb48_ToRgba32()
        {
            // arrange
            var rgba48 = new Rgb48(0.08f, 0.15f, 0.30f);
            var actual = default(Rgba32);
            var expected = new Rgba32(20, 38, 76, 255);

            // act
            rgba48.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgb48_ToArgb32()
        {
            // arrange
            var rgba48 = new Rgb48(0.08f, 0.15f, 0.30f);
            var actual = default(Argb32);
            var expected = new Argb32(20, 38, 76, 255);

            // act
            rgba48.ToArgb32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgba64_ToBgr24()
        {
            // arrange
            var rgb48 = new Rgb48(0.08f, 0.15f, 0.30f);
            var actual = default(Bgr24);
            var expected = new Bgr24(20, 38, 76);

            // act
            rgb48.ToBgr24(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgb48_ToBgra32()
        {
            // arrange
            var rgba48 = new Rgb48(0.08f, 0.15f, 0.30f);
            var actual = default(Bgra32);
            var expected = new Bgra32(20, 38, 76, 255);

            // act
            rgba48.ToBgra32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgb48_PackFromRgba32_ToRgba32()
        {
            // arrange
            var rgb48 = default(Rgb48);
            var actual = default(Rgba32);
            var expected = new Rgba32(20, 38, 76, 255);

            // act 
            rgb48.PackFromRgba32(expected);
            rgb48.ToRgba32(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgb48_PackFromRgb48_ToRgb48()
        {
            // arrange
            var input = default(Rgb48);
            var actual = default(Rgb48);
            var expected = new Rgb48(65535, 0, 65535);

            // act
            input.PackFromRgb48(expected);
            input.ToRgb48(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Rgb48_PackFromRgba64_ToRgba64()
        {
            // arrange
            var input = default(Rgb48);
            var actual = default(Rgba64);
            var expected = new Rgba64(65535, 0, 65535, 65535);

            // act
            input.PackFromRgba64(expected);
            input.ToRgba64(ref actual);

            // assert
            Assert.Equal(expected, actual);
        }
    }
}
