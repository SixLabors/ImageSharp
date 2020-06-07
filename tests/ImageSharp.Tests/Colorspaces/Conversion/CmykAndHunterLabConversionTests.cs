// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="Cmyk"/>-<see cref="HunterLab"/> conversions.
    /// </summary>
    public class CmykAndHunterLabConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="Cmyk"/> to <see cref="HunterLab"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 99.99999, 0, -1.66893E-05)]
        [InlineData(0.360555, 0.1036901, 0.818514, 0.274615, 55.66742, -27.21679, 31.73834)]
        public void Convert_Cmyk_to_HunterLab(float c, float m, float y, float k, float l, float a, float b)
        {
            // Arrange
            var input = new Cmyk(c, m, y, k);
            var expected = new HunterLab(l, a, b);

            Span<Cmyk> inputSpan = new Cmyk[5];
            inputSpan.Fill(input);

            Span<HunterLab> actualSpan = new HunterLab[5];

            // Act
            var actual = Converter.ToHunterLab(input);
            Converter.Convert(inputSpan, actualSpan);

            // Assert
            Assert.Equal(expected, actual, ColorSpaceComparer);

            for (int i = 0; i < actualSpan.Length; i++)
            {
                Assert.Equal(expected, actualSpan[i], ColorSpaceComparer);
            }
        }

        /// <summary>
        /// Tests conversion from <see cref="HunterLab"/> to <see cref="Cmyk"/>.
        /// </summary>
        [Theory]
        [InlineData(99.99999, 0, -1.66893E-05, 1.192093E-07, 1.192093E-07, 0, 5.960464E-08)]
        [InlineData(55.66742, -27.21679, 31.73834, 0.2865806, 0, 0.7975186, 0.3498301)]
        public void Convert_HunterLab_to_Cmyk(float l, float a, float b, float c, float m, float y, float k)
        {
            // Arrange
            var input = new HunterLab(l, a, b);
            var expected = new Cmyk(c, m, y, k);

            Span<HunterLab> inputSpan = new HunterLab[5];
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