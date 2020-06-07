// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieLch"/>-<see cref="LinearRgb"/> conversions.
    /// </summary>
    public class CieLchAndLinearRgbConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="CieLch"/> to <see cref="LinearRgb"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(36.0555, 103.6901, 10.01514, 0.6765013, 0, 0.05209038)]
        public void Convert_CieLch_to_LinearRgb(float l, float c, float h, float r, float g, float b)
        {
            // Arrange
            var input = new CieLch(l, c, h);
            var expected = new LinearRgb(r, g, b);

            Span<CieLch> inputSpan = new CieLch[5];
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

        /// <summary>
        /// Tests conversion from <see cref="LinearRgb"/> to <see cref="CieLch"/>.
        /// </summary>
        [Theory]
        [InlineData(0.6765013, 0, 0.05209038, 46.13445, 78.06367, 22.90504)]
        public void Convert_LinearRgb_to_CieLch(float r, float g, float b, float l, float c, float h)
        {
            // Arrange
            var input = new LinearRgb(r, g, b);
            var expected = new CieLch(l, c, h);

            Span<LinearRgb> inputSpan = new LinearRgb[5];
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
    }
}