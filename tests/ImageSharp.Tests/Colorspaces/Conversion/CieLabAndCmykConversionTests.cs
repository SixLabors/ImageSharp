// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieLab"/>-<see cref="Cmyk"/> conversions.
    /// </summary>
    public class CieLabAndCmykConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="Cmyk"/> to <see cref="CieLab"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 1, 0, 0, 0)]
        [InlineData(0, 1, 0.6156551, 5.960464E-08, 55.063, 82.54871, 23.16506)]
        public void Convert_Cmyk_to_CieLab(float c, float m, float y, float k, float l, float a, float b)
        {
            // Arrange
            var input = new Cmyk(c, m, y, k);
            var expected = new CieLab(l, a, b);

            Span<Cmyk> inputSpan = new Cmyk[5];
            inputSpan.Fill(input);

            Span<CieLab> actualSpan = new CieLab[5];

            // Act
            var actual = Converter.ToCieLab(input);
            Converter.Convert(inputSpan, actualSpan);

            // Assert
            Assert.Equal(expected, actual, ColorSpaceComparer);

            for (int i = 0; i < actualSpan.Length; i++)
            {
                Assert.Equal(expected, actualSpan[i], ColorSpaceComparer);
            }
        }

        /// <summary>
        /// Tests conversion from <see cref="CieLab"/> to <see cref="Cmyk"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0, 1)]
        [InlineData(36.0555, 303.6901, 10.01514, 0, 1, 0.6156551, 5.960464E-08)]
        public void Convert_CieLab_to_Cmyk(float l, float a, float b, float c, float m, float y, float k)
        {
            // Arrange
            var input = new CieLab(l, a, b);
            var expected = new Cmyk(c, m, y, k);

            Span<CieLab> inputSpan = new CieLab[5];
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