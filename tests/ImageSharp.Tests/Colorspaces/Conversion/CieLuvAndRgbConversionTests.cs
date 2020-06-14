// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieLuv"/>-<see cref="Rgb"/> conversions.
    /// </summary>
    public class CieLuvAndRgbConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="CieLuv"/> to <see cref="Rgb"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(36.0555, 93.6901, 10.01514, 0.6444615, 0.1086071, 0.2213444)]
        public void Convert_CieLuv_to_Rgb(float l, float u, float v, float r, float g, float b)
        {
            // Arrange
            var input = new CieLuv(l, u, v);
            var expected = new Rgb(r, g, b);

            Span<CieLuv> inputSpan = new CieLuv[5];
            inputSpan.Fill(input);

            Span<Rgb> actualSpan = new Rgb[5];

            // Act
            var actual = Converter.ToRgb(input);
            Converter.Convert(inputSpan, actualSpan);

            // Assert
            Assert.Equal(expected, actual, ColorSpaceComparer);

            for (int i = 0; i < actualSpan.Length; i++)
            {
                Assert.Equal(expected, actualSpan[i], ColorSpaceComparer);
            }
        }

        /// <summary>
        /// Tests conversion from <see cref="Rgb"/> to <see cref="CieLuv"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(0.6444615, 0.1086071, 0.2213444, 36.0555, 93.69012, 10.01514)]
        public void Convert_Rgb_to_CieLuv(float r, float g, float b, float l, float u, float v)
        {
            // Arrange
            var input = new Rgb(r, g, b);
            var expected = new CieLuv(l, u, v);

            Span<Rgb> inputSpan = new Rgb[5];
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
