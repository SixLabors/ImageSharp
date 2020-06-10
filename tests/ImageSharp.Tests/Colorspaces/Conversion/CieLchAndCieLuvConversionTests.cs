// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieLch"/>-<see cref="CieLuv"/> conversions.
    /// </summary>
    public class CieLchAndCieLuvConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="CieLch"/> to <see cref="CieLuv"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(36.0555, 103.6901, 10.01514, 34.89777, 187.6642, -7.181467)]
        public void Convert_CieLch_to_CieLuv(float l, float c, float h, float l2, float u, float v)
        {
            // Arrange
            var input = new CieLch(l, c, h);
            var expected = new CieLuv(l2, u, v);

            Span<CieLch> inputSpan = new CieLch[5];
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
        /// Tests conversion from <see cref="CieLuv"/> to <see cref="CieLch"/>.
        /// </summary>
        [Theory]
        [InlineData(34.89777, 187.6642, -7.181467, 36.05552, 103.6901, 10.01514)]
        public void Convert_CieLuv_to_CieLch(float l2, float u, float v, float l, float c, float h)
        {
            // Arrange
            var input = new CieLuv(l2, u, v);
            var expected = new CieLch(l, c, h);

            Span<CieLuv> inputSpan = new CieLuv[5];
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