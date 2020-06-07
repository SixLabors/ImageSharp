// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieXyz"/>-<see cref="YCbCr"/> conversions.
    /// </summary>
    public class CieXyzAndYCbCrConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="CieXyz"/> to <see cref="YCbCr"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 128, 128)]
        [InlineData(0.360555, 0.936901, 0.1001514, 149.685, 43.52769, 21.23457)]
        public void Convert_CieXyz_to_YCbCr(float x, float y, float z, float y2, float cb, float cr)
        {
            // Arrange
            var input = new CieXyz(x, y, z);
            var expected = new YCbCr(y2, cb, cr);

            Span<CieXyz> inputSpan = new CieXyz[5];
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
        /// Tests conversion from <see cref="YCbCr"/> to <see cref="CieXyz"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 128, 128, 0, 0, 0)]
        [InlineData(149.685, 43.52769, 21.23457, 0.3575761, 0.7151522, 0.119192)]
        public void Convert_YCbCr_to_CieXyz(float y2, float cb, float cr, float x, float y, float z)
        {
            // Arrange
            var input = new YCbCr(y2, cb, cr);
            var expected = new CieXyz(x, y, z);

            Span<YCbCr> inputSpan = new YCbCr[5];
            inputSpan.Fill(input);

            Span<CieXyz> actualSpan = new CieXyz[5];

            // Act
            CieXyz actual = Converter.ToCieXyz(input);
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
