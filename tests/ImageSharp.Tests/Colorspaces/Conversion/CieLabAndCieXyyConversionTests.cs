// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieLab"/>-<see cref="CieXyy"/> conversions.
    /// </summary>
    public class CieLabAndCieXyyConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="CieXyy"/> to <see cref="CieLab"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(0.8644734, 0.06098868, 0.06509002, 36.05552, 275.6228, 10.01517)]
        public void Convert_CieXyy_to_CieLab(float x, float y, float yl, float l, float a, float b)
        {
            // Arrange
            var input = new CieXyy(x, y, yl);
            var expected = new CieLab(l, a, b);

            Span<CieXyy> inputSpan = new CieXyy[5];
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
        /// Tests conversion from <see cref="CieLab"/> to <see cref="CieXyy"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(36.0555, 303.6901, 10.01514, 0.8644734, 0.06098868, 0.06509002)]
        public void Convert_CieLab_to_CieXyy(float l, float a, float b, float x, float y, float yl)
        {
            // Arrange
            var input = new CieLab(l, a, b);
            var expected = new CieXyy(x, y, yl);

            Span<CieLab> inputSpan = new CieLab[5];
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
    }
}
