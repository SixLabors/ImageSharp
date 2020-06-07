// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="Cmyk"/>-<see cref="Hsl"/> conversions.
    /// </summary>
    public class CmykAndHslConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="Cmyk"/> to <see cref="Hsl"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0, 1)]
        [InlineData(0.360555, 0.1036901, 0.818514, 0.274615, 81.56041, 0.6632275, 0.3909085)]
        public void Convert_Cmyk_to_Hsl(float c, float m, float y, float k, float h, float s, float l)
        {
            // Arrange
            var input = new Cmyk(c, m, y, k);
            var expected = new Hsl(h, s, l);

            Span<Cmyk> inputSpan = new Cmyk[5];
            inputSpan.Fill(input);

            Span<Hsl> actualSpan = new Hsl[5];

            // Act
            var actual = Converter.ToHsl(input);
            Converter.Convert(inputSpan, actualSpan);

            // Assert
            Assert.Equal(expected, actual, ColorSpaceComparer);

            for (int i = 0; i < actualSpan.Length; i++)
            {
                Assert.Equal(expected, actualSpan[i], ColorSpaceComparer);
            }
        }

        /// <summary>
        /// Tests conversion from <see cref="Hsl"/> to <see cref="Cmyk"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 1, 0, 0, 0, 0)]
        [InlineData(81.56041, 0.6632275, 0.3909085, 0.2865805, 0, 0.7975187, 0.3498302)]
        public void Convert_Hsl_to_Cmyk(float h, float s, float l, float c, float m, float y, float k)
        {
            // Arrange
            var input = new Hsl(h, s, l);
            var expected = new Cmyk(c, m, y, k);

            Span<Hsl> inputSpan = new Hsl[5];
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