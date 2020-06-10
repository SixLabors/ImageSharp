// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieLchuv"/>-<see cref="Cmyk"/> conversions.
    /// </summary>
    public class CieLchuvAndCmykConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="Cmyk"/> to <see cref="CieLchuv"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 1, 0, 0, 0)]
        [InlineData(0, 0.8576171, 0.7693201, 0.3440427, 36.0555, 103.6901, 10.01514)]
        public void Convert_Cmyk_to_CieLchuv(float c2, float m, float y, float k, float l, float c, float h)
        {
            // Arrange
            var input = new Cmyk(c2, m, y, k);
            var expected = new CieLchuv(l, c, h);

            Span<Cmyk> inputSpan = new Cmyk[5];
            inputSpan.Fill(input);

            Span<CieLchuv> actualSpan = new CieLchuv[5];

            // Act
            var actual = Converter.ToCieLchuv(input);
            Converter.Convert(inputSpan, actualSpan);

            // Assert
            Assert.Equal(expected, actual, ColorSpaceComparer);

            for (int i = 0; i < actualSpan.Length; i++)
            {
                Assert.Equal(expected, actualSpan[i], ColorSpaceComparer);
            }
        }

        /// <summary>
        /// Tests conversion from <see cref="CieLchuv"/> to <see cref="Cmyk"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0, 1)]
        [InlineData(36.0555, 103.6901, 10.01514, 0, 0.8576171, 0.7693201, 0.3440427)]
        public void Convert_CieLchuv_to_Cmyk(float l, float c, float h, float c2, float m, float y, float k)
        {
            // Arrange
            var input = new CieLchuv(l, c, h);
            var expected = new Cmyk(c2, m, y, k);

            Span<CieLchuv> inputSpan = new CieLchuv[5];
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