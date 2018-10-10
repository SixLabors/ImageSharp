// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Tests.Helpers
{
    using Xunit;

    public class ImageMathsTests
    {
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