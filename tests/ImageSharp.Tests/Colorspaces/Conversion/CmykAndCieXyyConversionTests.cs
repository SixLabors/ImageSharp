// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="Cmyk"/>-<see cref="CieXyy"/> conversions.
    /// </summary>
    public class CmykAndCieXyyConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="Cmyk"/> to <see cref="CieXyy"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0.3127266, 0.3290231, 1)]
        [InlineData(0.360555, 0.1036901, 0.818514, 0.274615, 0.3628971, 0.5289949, 0.3118104)]
        public void Convert_Cmyk_to_CieXyy(float c, float m, float y, float k, float x, float y2, float yl)
        {
            // Arrange
            var input = new Cmyk(c, m, y, k);
            var expected = new CieXyy(x, y2, yl);

            Span<Cmyk> inputSpan = new Cmyk[5];
            inputSpan.Fill(input);

            Span<CieXyy> actualSpan = new CieXyy[5];

            // Act
            var actual = Converter.ToCieXyy(input);
            Converter.Convert(inputSpan, actualSpan);

            // Assert
            Assert.Equal(expected, actual, ColorSpaceComparer);

            for (int i = 0; i < actualSpan.Length; i++)
            {
                Assert.Equal(expected, actualSpan[i], ColorSpaceComparer);
            }
        }

        /// <summary>
        /// Tests conversion from <see cref="CieXyy"/> to <see cref="Cmyk"/>.
        /// </summary>
        [Theory]
        [InlineData(0.3127266, 0.3290231, 1, 0, 0, 0, 5.960464E-08)]
        [InlineData(0.3628971, 0.5289949, 0.3118104, 0.2865805, 0, 0.7975187, 0.3498302)]
        public void Convert_CieXyy_to_Cmyk(float x, float y2, float yl, float c, float m, float y, float k)
        {
            // Arrange
            var input = new CieXyy(x, y2, yl);
            var expected = new Cmyk(c, m, y, k);

            Span<CieXyy> inputSpan = new CieXyy[5];
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