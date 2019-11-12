// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Memory;

using Xunit;

namespace SixLabors.ImageSharp.Tests.Helpers
{
    public class ImageMathsTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(100)]
        [InlineData(123)]
        [InlineData(53436353)]
        public void Modulo4(int x)
        {
            int actual = ImageMaths.Modulo4(x);
            Assert.Equal(x % 4, actual);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(100)]
        [InlineData(123)]
        [InlineData(53436353)]
        [InlineData(975)]
        public void Modulo8(int x)
        {
            int actual = ImageMaths.Modulo8(x);
            Assert.Equal(x % 8, actual);
        }

        [Theory]
        [InlineData(0, 2)]
        [InlineData(1, 2)]
        [InlineData(2, 2)]
        [InlineData(0, 4)]
        [InlineData(3, 4)]
        [InlineData(5, 4)]
        [InlineData(5, 8)]
        [InlineData(8, 8)]
        [InlineData(8, 16)]
        [InlineData(15, 16)]
        [InlineData(17, 16)]
        [InlineData(17, 32)]
        [InlineData(31, 32)]
        [InlineData(32, 32)]
        [InlineData(33, 32)]
        public void Modulo2P(int x, int m)
        {
            int actual = ImageMaths.ModuloP2(x, m);
            Assert.Equal(x % m, actual);
        }

        [Theory]
        [InlineData(0, 0, 0, 0)]
        [InlineData(0.5f, 0, 1, 0.5f)]
        [InlineData(-0.5f, -0.1f, 10, -0.1f)]
        [InlineData(-0.05f, -0.1f, 10, -0.05f)]
        [InlineData(9.9f, -0.1f, 10, 9.9f)]
        [InlineData(10f, -0.1f, 10, 10f)]
        [InlineData(10.1f, -0.1f, 10, 10f)]
        public void Clamp(float x, float min, float max, float expected)
        {
            float actual = x.Clamp(min, max);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void FasAbsResultMatchesMath()
        {
            const int X = -33;
            int expected = Math.Abs(X);

            Assert.Equal(expected, ImageMaths.FastAbs(X));
        }

        [Fact]
        public void Pow2ResultMatchesMath()
        {
            const float X = -33;
            float expected = (float)Math.Pow(X, 2);

            Assert.Equal(expected, ImageMaths.Pow2(X));
        }

        [Fact]
        public void Pow3ResultMatchesMath()
        {
            const float X = -33;
            float expected = (float)Math.Pow(X, 3);

            Assert.Equal(expected, ImageMaths.Pow3(X));
        }

        [Theory]
        [InlineData(1, 1, 1)]
        [InlineData(1, 42, 1)]
        [InlineData(10, 8, 2)]
        [InlineData(12, 18, 6)]
        [InlineData(4536, 1000, 8)]
        [InlineData(1600, 1024, 64)]
        public void GreatestCommonDivisor(int a, int b, int expected)
        {
            int actual = ImageMaths.GreatestCommonDivisor(a, b);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(1, 1, 1)]
        [InlineData(1, 42, 42)]
        [InlineData(3, 4, 12)]
        [InlineData(6, 4, 12)]
        [InlineData(1600, 1024, 25600)]
        [InlineData(3264, 100, 81600)]
        public void LeastCommonMultiple(int a, int b, int expected)
        {
            int actual = ImageMaths.LeastCommonMultiple(a, b);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(0.2f, 0.7f, 0.1f, 256, 140)]
        [InlineData(0.5f, 0.5f, 0.5f, 256, 128)]
        [InlineData(0.5f, 0.5f, 0.5f, 65536, 32768)]
        [InlineData(0.2f, 0.7f, 0.1f, 65536, 36069)]
        public void GetBT709Luminance_WithVector4(float x, float y, float z, int luminanceLevels, int expected)
        {
            // arrange
            var vector = new Vector4(x, y, z, 0.0f);

            // act
            int actual = ImageMaths.GetBT709Luminance(ref vector, luminanceLevels);

            // assert
            Assert.Equal(expected, actual);
        }

        // TODO: We need to test all ImageMaths methods!
    }
}
