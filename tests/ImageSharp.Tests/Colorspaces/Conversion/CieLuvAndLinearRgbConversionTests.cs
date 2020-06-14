// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieLuv"/>-<see cref="LinearRgb"/> conversions.
    /// </summary>
    public class CieLuvAndLinearRgbConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="CieLuv"/> to <see cref="LinearRgb"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(36.0555, 93.6901, 10.01514, 0.3729299, 0.01141088, 0.04014909)]
        public void Convert_CieLuv_to_LinearRgb(float l, float u, float v, float r, float g, float b)
        {
            // Arrange
            var input = new CieLuv(l, u, v);
            var expected = new LinearRgb(r, g, b);

            Span<CieLuv> inputSpan = new CieLuv[5];
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
        /// Tests conversion from <see cref="LinearRgb"/> to <see cref="CieLuv"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(0.3729299, 0.01141088, 0.04014909, 36.0555, 93.6901, 10.01511)]
        public void Convert_LinearRgb_to_CieLuv(float r, float g, float b, float l, float u, float v)
        {
            // Arrange
            var input = new LinearRgb(r, g, b);
            var expected = new CieLuv(l, u, v);

            Span<LinearRgb> inputSpan = new LinearRgb[5];
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
    }
}
