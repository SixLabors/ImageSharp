// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieXyz"/>-<see cref="Hsv"/> conversions.
    /// </summary>
    public class CieXyzAndHsvConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="CieXyz"/> to <see cref="Hsv"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(0.360555, 0.936901, 0.1001514, 120, 1, 0.9999999)]
        public void Convert_CieXyz_to_Hsv(float x, float y, float yl, float h, float s, float v)
        {
            // Arrange
            var input = new CieXyz(x, y, yl);
            var expected = new Hsv(h, s, v);

            Span<CieXyz> inputSpan = new CieXyz[5];
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
        /// Tests conversion from <see cref="Hsv"/> to <see cref="CieXyz"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(120, 1, 0.9999999, 0.3575761, 0.7151522, 0.119192)]
        public void Convert_Hsv_to_CieXyz(float h, float s, float v, float x, float y, float yl)
        {
            // Arrange
            var input = new Hsv(h, s, v);
            var expected = new CieXyz(x, y, yl);

            Span<Hsv> inputSpan = new Hsv[5];
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
