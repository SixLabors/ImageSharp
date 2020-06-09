// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieLab"/>-<see cref="LinearRgb"/> conversions.
    /// </summary>
    public class CieLabAndLinearRgbConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="LinearRgb"/> to <see cref="CieLab"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(1, 0, 0.1221596, 55.063, 82.54871, 23.16505)]
        public void Convert_LinearRgb_to_CieLab(float r, float g, float b2, float l, float a, float b)
        {
            // Arrange
            var input = new LinearRgb(r, g, b2);
            var expected = new CieLab(l, a, b);

            Span<LinearRgb> inputSpan = new LinearRgb[5];
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
        /// Tests conversion from <see cref="CieLab"/> to <see cref="LinearRgb"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(36.0555, 303.6901, 10.01514, 1, 0, 0.1221596)]
        public void Convert_CieLab_to_LinearRgb(float l, float a, float b, float r, float g, float b2)
        {
            // Arrange
            var input = new CieLab(l, a, b);
            var expected = new LinearRgb(r, g, b2);

            Span<CieLab> inputSpan = new CieLab[5];
            inputSpan.Fill(input);

            Span<LinearRgb> actualSpan = new LinearRgb[5];

            // Act
            var actual = Converter.ToLinearRgb(input);
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