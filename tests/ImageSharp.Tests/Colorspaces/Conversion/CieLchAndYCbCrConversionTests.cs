// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieLch"/>-<see cref="YCbCr"/> conversions.
    /// </summary>
    public class CieLchAndYCbCrConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="CieLch"/> to <see cref="YCbCr"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 128, 128)]
        [InlineData(36.0555, 103.6901, 10.01514, 71.5122, 124.053, 230.0401)]
        public void Convert_CieLch_to_YCbCr(float l, float c, float h, float y, float cb, float cr)
        {
            // Arrange
            var input = new CieLch(l, c, h);
            var expected = new YCbCr(y, cb, cr);

            Span<CieLch> inputSpan = new CieLch[5];
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
        /// Tests conversion from <see cref="YCbCr"/> to <see cref="CieLch"/>.
        /// </summary>
        [Theory]
        [InlineData(71.5122, 124.053, 230.0401, 46.23178, 78.1114, 22.7662)]
        public void Convert_YCbCr_to_CieLch(float y, float cb, float cr, float l, float c, float h)
        {
            // Arrange
            var input = new YCbCr(y, cb, cr);
            var expected = new CieLch(l, c, h);

            Span<YCbCr> inputSpan = new YCbCr[5];
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