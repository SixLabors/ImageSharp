// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieLab"/>-<see cref="YCbCr"/> conversions.
    /// </summary>
    public class CieLabAndYCbCrConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="YCbCr"/> to <see cref="CieLab"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 128, 128, 0, 0, 0)]
        [InlineData(87.4179, 133.9763, 247.5308, 55.06287, 82.54838, 23.1697)]
        public void Convert_YCbCr_to_CieLab(float y, float cb, float cr, float l, float a, float b)
        {
            // Arrange
            var input = new YCbCr(y, cb, cr);
            var expected = new CieLab(l, a, b);

            Span<YCbCr> inputSpan = new YCbCr[5];
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
        /// Tests conversion from <see cref="CieLab"/> to <see cref="YCbCr"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 128, 128)]
        [InlineData(36.0555, 303.6901, 10.01514, 87.4179, 133.9763, 247.5308)]
        public void Convert_CieLab_to_YCbCr(float l, float a, float b, float y, float cb, float cr)
        {
            // Arrange
            var input = new CieLab(l, a, b);
            var expected = new YCbCr(y, cb, cr);

            Span<CieLab> inputSpan = new CieLab[5];
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
    }
}