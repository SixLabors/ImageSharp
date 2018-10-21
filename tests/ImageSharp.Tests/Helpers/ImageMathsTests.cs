// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Tests.Helpers
{
    using Xunit;

    public class ImageMathsTests
    {
        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 1)]
        [InlineData(2, 2)]
        [InlineData(3, 3)]
        [InlineData(4, 0)]
        [InlineData(100, 0)]
        [InlineData(123, 3)]
        [InlineData(53436353, 1)]
        public void Modulo4(int a, int expected)
        {
            int actual = ImageMaths.Modulo4(a);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 1)]
        [InlineData(2, 2)]
        [InlineData(6, 6)]
        [InlineData(7, 7)]
        [InlineData(8, 0)]
        [InlineData(100, 4)]
        [InlineData(123, 3)]
        [InlineData(53436353, 1)]
        [InlineData(975, 7)]
        public void Modulo8(int a, int expected)
        {
            int actual = ImageMaths.Modulo8(a);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData(0, 2, 0)]
        [InlineData(1, 2, 1)]
        [InlineData(2, 2, 0)]
        [InlineData(0, 4, 0)]
        [InlineData(3, 4, 3)]
        [InlineData(5, 4, 1)]
        [InlineData(5, 8, 5)]
        [InlineData(8, 8, 0)]
        [InlineData(8, 16, 8)]
        [InlineData(15, 16, 15)]
        [InlineData(17, 16, 1)]
        [InlineData(17, 32, 17)]
        [InlineData(31, 32, 31)]
        [InlineData(32, 32, 0)]
        [InlineData(33, 32, 1)]
        public void Modulo2P(int a, int m, int expected)
        {
            int actual = ImageMaths.ModuloP2(a, m);
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

        // TODO: We need to test all ImageMaths methods!
    }
}