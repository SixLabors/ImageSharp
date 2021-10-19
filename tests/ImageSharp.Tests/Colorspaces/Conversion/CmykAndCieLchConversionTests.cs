﻿// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="Cmyk"/>-<see cref="CieLch"/> conversions.
    /// </summary>
    public class CmykAndCieLchConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="Cmyk"/> to <see cref="CieLch"/>.
        /// </summary>
        [Theory]
        [InlineData(0.360555, 0.1036901, 0.818514, 0.274615, 62.85025, 64.77041, 118.2425)]
        public void Convert_Cmyk_to_CieLch(float c, float m, float y, float k, float l, float c2, float h)
        {
            // Arrange
            var input = new Cmyk(c, m, y, k);
            var expected = new CieLch(l, c2, h);

            Span<Cmyk> inputSpan = new Cmyk[5];
            inputSpan.Fill(input);

            Span<CieLch> actualSpan = new CieLch[5];

            // Act
            var actual = Converter.ToCieLch(input);
            Converter.Convert(inputSpan, actualSpan);

            // Assert
            Assert.Equal(expected, actual, ColorSpaceComparer);

            for (int i = 0; i < actualSpan.Length; i++)
            {
                Assert.Equal(expected, actualSpan[i], ColorSpaceComparer);
            }
        }

        /// <summary>
        /// Tests conversion from <see cref="CieLch"/> to <see cref="Cmyk"/>.
        /// </summary>
        [Theory]
        [InlineData(100, 3.81656E-05, 218.6598, 0, 1.192093E-07, 0, 5.960464E-08)]
        [InlineData(62.85025, 64.77041, 118.2425, 0.286581, 0, 0.7975187, 0.34983)]
        public void Convert_CieLch_to_Cmyk(float l, float c2, float h, float c, float m, float y, float k)
        {
            // Arrange
            var input = new CieLch(l, c2, h);
            var expected = new Cmyk(c, m, y, k);

            Span<CieLch> inputSpan = new CieLch[5];
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
