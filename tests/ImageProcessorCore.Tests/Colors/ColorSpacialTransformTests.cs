// <copyright file="MultiplyTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore.Tests
{
    using Xunit;

    public class ColorSpacialTransformTests
    {
        public class MultiplyTests
        {
            [Fact]
            public void MultiplyBlendConvertsRedBackdropAndGreenOverlayToBlack()
            {
                var backdrop = Color.Red;
                var overlay = Color.Green;

                var result = Color.Multiply(backdrop, overlay);

                Assert.Equal(Color.Black, result);
            }
            [Fact]
            public void MultiplyBlendConvertsBlueBackdropAndWhiteOverlayToBlue()
            {
                var backdrop = Color.Blue;
                var overlay = Color.White;

                var result = Color.Multiply(backdrop, overlay);

                Assert.Equal(Color.Blue, result);
            }
            [Fact]
            public void MultiplyBlendConvertsBlueBackdropAndBlackOverlayToBlack()
            {
                var backdrop = Color.Blue;
                var overlay = Color.Black;

                var result = Color.Multiply(backdrop, overlay);

                Assert.Equal(Color.Black, result);
            }
            [Fact]
            public void MultiplyBlendConvertsBlueBackdropAndGrayOverlayToBlueBlack()
            {
                var backdrop = Color.Blue;
                var overlay = Color.Gray;

                var result = Color.Multiply(backdrop, overlay);

                var expected = new Color(0, 0, 0.5f, 1);

                Assert.True(expected.AlmostEquals(result,.01f));
            }
        }
    }
}