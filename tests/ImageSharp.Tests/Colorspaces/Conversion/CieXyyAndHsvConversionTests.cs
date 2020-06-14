// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieXyy"/>-<see cref="Hsv"/> conversions.
    /// </summary>
    public class CieXyyAndHsvConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="CieXyy"/> to <see cref="Hsv"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(0.360555, 0.936901, 0.1001514, 120, 1, 0.4225259)]
        public void Convert_CieXyy_to_Hsv(float x, float y, float yl, float h, float s, float v)
        {
            // Arrange
            var input = new CieXyy(x, y, yl);
            var expected = new Hsv(h, s, v);

            Span<CieXyy> inputSpan = new CieXyy[5];
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

        /// <summary>
        /// Tests conversion from <see cref="Hsv"/> to <see cref="CieXyy"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(120, 1, 0.4225259, 0.3, 0.6, 0.1067051)]
        public void Convert_Hsv_to_CieXyy(float h, float s, float v, float x, float y, float yl)
        {
            // Arrange
            var input = new Hsv(h, s, v);
            var expected = new CieXyy(x, y, yl);

            Span<Hsv> inputSpan = new Hsv[5];
            inputSpan.Fill(input);

            Span<CieXyy> actualSpan = new CieXyy[5];

            // Act
            var actual = Converter.ToCieXyy(input);
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
