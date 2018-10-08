// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using Xunit;

namespace SixLabors.ImageSharp.Tests.Helpers
{
    public class ImageMathsTests
    {
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