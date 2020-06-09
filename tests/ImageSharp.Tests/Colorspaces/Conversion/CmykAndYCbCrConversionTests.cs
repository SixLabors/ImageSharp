// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="Cmyk"/>-<see cref="YCbCr"/> conversions.
    /// </summary>
    public class CmykAndYCbCrConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="Cmyk"/> to <see cref="YCbCr"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 255, 128, 128)]
        [InlineData(0.360555, 0.1036901, 0.818514, 0.274615, 136.5134, 69.90555, 114.9948)]
        public void Convert_Cmyk_to_YCbCr(float c, float m, float y, float k, float y2, float cb, float cr)
        {
            // Arrange
            var input = new Cmyk(c, m, y, k);
            var expected = new YCbCr(y2, cb, cr);

            Span<Cmyk> inputSpan = new Cmyk[5];
            inputSpan.Fill(input);

            Span<YCbCr> actualSpan = new YCbCr[5];

            // Act
            var actual = Converter.ToYCbCr(input);
            Converter.Convert(inputSpan, actualSpan);

            // Assert
            Assert.Equal(expected, actual, ColorSpaceComparer);

            for (int i = 0; i < actualSpan.Length; i++)
            {
                Assert.Equal(expected, actualSpan[i], ColorSpaceComparer);
            }
        }

        /// <summary>
        /// Tests conversion from <see cref="YCbCr"/> to <see cref="Cmyk"/>.
        /// </summary>
        [Theory]
        [InlineData(255, 128, 128, 0, 0, 0, 5.960464E-08)]
        [InlineData(136.5134, 69.90555, 114.9948, 0.2891567, 0, 0.7951807, 0.3490196)]
        public void Convert_YCbCr_to_Cmyk(float y2, float cb, float cr, float c, float m, float y, float k)
        {
            // Arrange
            var input = new YCbCr(y2, cb, cr);
            var expected = new Cmyk(c, m, y, k);

            Span<YCbCr> inputSpan = new YCbCr[5];
            inputSpan.Fill(input);

            Span<Cmyk> actualSpan = new Cmyk[5];

            // Act
            var actual = Converter.ToCmyk(input);
            Converter.Convert(inputSpan, actualSpan);

            // Assert
            Assert.Equal(expected, actual, ColorSpaceComparer);

            for (int i = 0; i < actualSpan.Length; i++)
            {
                Assert.Equal(expected, actualSpan[i], ColorSpaceComparer);
            }
        }
    }
}