// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="Cmyk"/>-<see cref="CieLuv"/> conversions.
    /// </summary>
    public class CmykAndCieLuvConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="Cmyk"/> to <see cref="CieLuv"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 100, -1.937151E-05, 0)]
        [InlineData(0.360555, 0.1036901, 0.818514, 0.274615, 62.66017, -24.01712, 68.29556)]
        public void Convert_Cmyk_to_CieLuv(float c, float m, float y, float k, float l, float u, float v)
        {
            // Arrange
            var input = new Cmyk(c, m, y, k);
            var expected = new CieLuv(l, u, v);

            Span<Cmyk> inputSpan = new Cmyk[5];
            inputSpan.Fill(input);

            Span<CieLuv> actualSpan = new CieLuv[5];

            // Act
            var actual = Converter.ToCieLuv(input);
            Converter.Convert(inputSpan, actualSpan);

            // Assert
            Assert.Equal(expected, actual, ColorSpaceComparer);

            for (int i = 0; i < actualSpan.Length; i++)
            {
                Assert.Equal(expected, actualSpan[i], ColorSpaceComparer);
            }
        }

        /// <summary>
        /// Tests conversion from <see cref="CieLuv"/> to <see cref="Cmyk"/>.
        /// </summary>
        [Theory]
        [InlineData(100, -1.937151E-05, 0, 3.576279E-07, 0, 0, 5.960464E-08)]
        [InlineData(62.66017, -24.01712, 68.29556, 0.2865804, 0, 0.7975189, 0.3498302)]
        public void Convert_CieLuv_to_Cmyk(float l, float u, float v, float c, float m, float y, float k)
        {
            // Arrange
            var input = new CieLuv(l, u, v);
            var expected = new Cmyk(c, m, y, k);

            Span<CieLuv> inputSpan = new CieLuv[5];
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