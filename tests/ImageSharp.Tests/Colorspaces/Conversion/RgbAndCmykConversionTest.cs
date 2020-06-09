// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="Rgb"/>-<see cref="Cmyk"/> conversions.
    /// </summary>
    /// <remarks>
    /// Test data generated using:
    /// <see href="http://www.colorhexa.com"/>
    /// <see href="http://www.rapidtables.com/convert/color/cmyk-to-rgb.htm"/>
    /// </remarks>
    public class RgbAndCmykConversionTest
    {
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0001F);

        /// <summary>
        /// Tests conversion from <see cref="Cmyk"/> to <see cref="Rgb"/>.
        /// </summary>
        [Theory]
        [InlineData(1, 1, 1, 1, 0, 0, 0)]
        [InlineData(0, 0, 0, 0, 1, 1, 1)]
        [InlineData(0, 0.84, 0.037, 0.365, 0.635, 0.1016, 0.6115)]
        public void Convert_Cmyk_To_Rgb(float c, float m, float y, float k, float r, float g, float b)
        {
            // Arrange
            var input = new Cmyk(c, m, y, k);
            var expected = new Rgb(r, g, b);

            Span<Cmyk> inputSpan = new Cmyk[5];
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
        /// Tests conversion from <see cref="Rgb"/> to <see cref="Cmyk"/>.
        /// </summary>
        [Theory]
        [InlineData(1, 1, 1, 0, 0, 0, 0)]
        [InlineData(0, 0, 0, 0, 0, 0, 1)]
        [InlineData(0.635, 0.1016, 0.6115, 0, 0.84, 0.037, 0.365)]
        public void Convert_Rgb_To_Cmyk(float r, float g, float b, float c, float m, float y, float k)
        {
            // Arrange
            var input = new Rgb(r, g, b);
            var expected = new Cmyk(c, m, y, k);

            Span<Rgb> inputSpan = new Rgb[5];
            inputSpan.Fill(input);

            Span<Cmyk> actualSpan = new Cmyk[5];

            // Act
            var actual = Converter.ToCmyk(input);
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
