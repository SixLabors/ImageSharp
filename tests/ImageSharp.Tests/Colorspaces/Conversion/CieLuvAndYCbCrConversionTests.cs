// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieLuv"/>-<see cref="YCbCr"/> conversions.
    /// </summary>
    public class CieLuvAndYCbCrConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="CieLuv"/> to <see cref="YCbCr"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 128, 128)]
        [InlineData(36.0555, 93.6901, 10.01514, 71.8283, 119.3174, 193.9839)]
        public void Convert_CieLuv_to_YCbCr(float l, float u, float v, float y, float cb, float cr)
        {
            // Arrange
            var input = new CieLuv(l, u, v);
            var expected = new YCbCr(y, cb, cr);

            Span<CieLuv> inputSpan = new CieLuv[5];
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

        /// <summary>
        /// Tests conversion from <see cref="YCbCr"/> to <see cref="CieLuv"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 128, 128, 0, 0, 0)]
        [InlineData(71.8283, 119.3174, 193.9839, 36.00565, 93.44593, 10.2234)]
        public void Convert_YCbCr_to_CieLuv(float y, float cb, float cr, float l, float u, float v)
        {
            // Arrange
            var input = new YCbCr(y, cb, cr);
            var expected = new CieLuv(l, u, v);

            Span<YCbCr> inputSpan = new YCbCr[5];
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
