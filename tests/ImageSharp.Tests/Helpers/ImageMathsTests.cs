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
            float expected = MathF.Pow(X, 2);

            Assert.Equal(expected, ImageMaths.Pow2(X));
        }

        [Fact]
        public void Pow3ResultMatchesMath()
        {
            const float X = -33;
            float expected = MathF.Pow(X, 3);

            Assert.Equal(expected, ImageMaths.Pow3(X));
        }
    }
}
