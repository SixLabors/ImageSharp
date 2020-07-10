// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieXyy"/>-<see cref="YCbCr"/> conversions.
    /// </summary>
    public class CieXyyAndYCbCrConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="CieXyy"/> to <see cref="YCbCr"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 128, 128)]
        [InlineData(0.360555, 0.936901, 0.1001514, 63.24579, 92.30826, 82.88884)]
        public void Convert_CieXyy_to_YCbCr(float x, float y, float yl, float y2, float cb, float cr)
        {
            // Arrange
            var input = new CieXyy(x, y, yl);
            var expected = new YCbCr(y2, cb, cr);

            Span<CieXyy> inputSpan = new CieXyy[5];
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
        /// Tests conversion from <see cref="YCbCr"/> to <see cref="CieXyy"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 128, 128, 0, 0, 0)]
        [InlineData(63.24579, 92.30826, 82.88884, 0.3, 0.6, 0.1072441)]
        public void Convert_YCbCr_to_CieXyy(float y2, float cb, float cr, float x, float y, float yl)
        {
            // Arrange
            var input = new YCbCr(y2, cb, cr);
            var expected = new CieXyy(x, y, yl);

            Span<YCbCr> inputSpan = new YCbCr[5];
            inputSpan.Fill(input);

            Span<CieXyy> actualSpan = new CieXyy[5];

            // Act
            CieXyy actual = Converter.ToCieXyy(input);
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
