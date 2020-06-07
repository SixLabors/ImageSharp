// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieLuv"/>-<see cref="CieLchuv"/> conversions.
    /// </summary>
    /// <remarks>
    /// Test data generated using:
    /// <see href="http://www.brucelindbloom.com/index.html?ColorCalculator.html"/>
    /// </remarks>
    public class CieLchuvAndCieLuvConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0001F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="CieLchuv"/> to <see cref="CieLuv"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(54.2917, 106.8391, 40.8526, 54.2917, 80.8125, 69.8851)]
        [InlineData(100, 0, 0, 100, 0, 0)]
        [InlineData(100, 50, 180, 100, -50, 0)]
        [InlineData(10, 36.0555, 56.3099, 10, 20, 30)]
        [InlineData(10, 36.0555, 123.6901, 10, -20, 30)]
        [InlineData(10, 36.0555, 303.6901, 10, 20, -30)]
        [InlineData(10, 36.0555, 236.3099, 10, -20, -30)]
        public void Convert_CieLchuv_to_CieLuv(float l, float c, float h, float l2, float u, float v)
        {
            // Arrange
            var input = new CieLchuv(l, c, h);
            var expected = new CieLuv(l2, u, v);

            Span<CieLchuv> inputSpan = new CieLchuv[5];
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

        /// <summary>
        /// Tests conversion from <see cref="CieLuv"/> to <see cref="CieLchuv"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(54.2917, 80.8125, 69.8851, 54.2917, 106.8391, 40.8526)]
        [InlineData(100, 0, 0, 100, 0, 0)]
        [InlineData(100, -50, 0, 100, 50, 180)]
        [InlineData(10, 20, 30, 10, 36.0555, 56.3099)]
        [InlineData(10, -20, 30, 10, 36.0555, 123.6901)]
        [InlineData(10, 20, -30, 10, 36.0555, 303.6901)]
        [InlineData(10, -20, -30, 10, 36.0555, 236.3099)]
        [InlineData(37.3511, 24.1720, 16.0684, 37.3511, 29.0255, 33.6141)]
        public void Convert_CieLuv_to_CieLchuv(float l, float u, float v, float l2, float c, float h)
        {
            // Arrange
            var input = new CieLuv(l, u, v);
            var expected = new CieLchuv(l2, c, h);

            Span<CieLuv> inputSpan = new CieLuv[5];
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
    }
}
