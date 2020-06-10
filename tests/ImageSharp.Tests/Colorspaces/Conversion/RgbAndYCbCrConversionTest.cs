// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="Rgb"/>-<see cref="YCbCr"/> conversions.
    /// </summary>
    /// <remarks>
    /// Test data generated mathematically
    /// </remarks>
    public class RgbAndYCbCrConversionTest
    {
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.001F);

        /// <summary>
        /// Tests conversion from <see cref="YCbCr"/> to <see cref="Rgb"/>.
        /// </summary>
        [Theory]
        [InlineData(255, 128, 128, 1, 1, 1)]
        [InlineData(0, 128, 128, 0, 0, 0)]
        [InlineData(128, 128, 128, 0.502, 0.502, 0.502)]
        public void Convert_YCbCr_To_Rgb(float y, float cb, float cr, float r, float g, float b)
        {
            // Arrange
            var input = new YCbCr(y, cb, cr);
            var expected = new Rgb(r, g, b);

            Span<YCbCr> inputSpan = new YCbCr[5];
            inputSpan.Fill(input);

            Span<Rgb> actualSpan = new Rgb[5];

            // Act
            var actual = Converter.ToRgb(input);
            Converter.Convert(inputSpan, actualSpan);

            // Assert
            Assert.Equal(Rgb.DefaultWorkingSpace, actual.WorkingSpace, ColorSpaceComparer);
            Assert.Equal(expected, actual, ColorSpaceComparer);

            for (int i = 0; i < actualSpan.Length; i++)
            {
                Assert.Equal(expected, actualSpan[i], ColorSpaceComparer);
            }
        }

        /// <summary>
        /// Tests conversion from <see cref="Rgb"/> to <see cref="YCbCr"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 128, 128)]
        [InlineData(1, 1, 1, 255, 128, 128)]
        [InlineData(0.5, 0.5, 0.5, 127.5, 128, 128)]
        [InlineData(1, 0, 0, 76.245, 84.972, 255)]
        public void Convert_Rgb_To_YCbCr(float r, float g, float b, float y, float cb, float cr)
        {
            // Arrange
            var input = new Rgb(r, g, b);
            var expected = new YCbCr(y, cb, cr);

            Span<Rgb> inputSpan = new Rgb[5];
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
    }
}