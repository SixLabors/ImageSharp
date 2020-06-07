// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="Rgb"/>-<see cref="Hsv"/> conversions.
    /// </summary>
    /// <remarks>
    /// Test data generated using:
    /// <see href="http://www.colorhexa.com"/>
    /// </remarks>
    public class RgbAndHsvConversionTest
    {
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0001F);

        /// <summary>
        /// Tests conversion from <see cref="Hsv"/> to <see cref="Rgb"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(0, 0, 1, 1, 1, 1)]
        [InlineData(360, 1, 1, 1, 0, 0)]
        [InlineData(0, 1, 1, 1, 0, 0)]
        [InlineData(120, 1, 1, 0, 1, 0)]
        [InlineData(240, 1, 1, 0, 0, 1)]
        public void Convert_Hsv_To_Rgb(float h, float s, float v, float r, float g, float b)
        {
            // Arrange
            var input = new Hsv(h, s, v);
            var expected = new Rgb(r, g, b);

            Span<Hsv> inputSpan = new Hsv[5];
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
        /// Tests conversion from <see cref="Rgb"/> to <see cref="Hsv"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(1, 1, 1, 0, 0, 1)]
        [InlineData(1, 0, 0, 0, 1, 1)]
        [InlineData(0, 1, 0, 120, 1, 1)]
        [InlineData(0, 0, 1, 240, 1, 1)]
        public void Convert_Rgb_To_Hsv(float r, float g, float b, float h, float s, float v)
        {
            // Arrange
            var input = new Rgb(r, g, b);
            var expected = new Hsv(h, s, v);

            Span<Rgb> inputSpan = new Rgb[5];
            inputSpan.Fill(input);

            Span<Hsv> actualSpan = new Hsv[5];

            // Act
            var actual = Converter.ToHsv(input);
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
