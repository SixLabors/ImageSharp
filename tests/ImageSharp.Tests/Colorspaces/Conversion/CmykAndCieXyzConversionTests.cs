// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="Cmyk"/>-<see cref="CieXyz"/> conversions.
    /// </summary>
    public class CmykAndCieXyzConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="Cmyk"/> to <see cref="CieXyz"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0.9504699, 1, 1.08883)]
        [InlineData(0.360555, 0.1036901, 0.818514, 0.274615, 0.2139058, 0.3118104, 0.0637231)]
        public void Convert_Cmyk_to_CieXyz(float c, float m, float y, float k, float x, float y2, float z)
        {
            // Arrange
            var input = new Cmyk(c, m, y, k);
            var expected = new CieXyz(x, y2, z);

            Span<Cmyk> inputSpan = new Cmyk[5];
            inputSpan.Fill(input);

            Span<CieXyz> actualSpan = new CieXyz[5];

            // Act
            var actual = Converter.ToCieXyz(input);
            Converter.Convert(inputSpan, actualSpan);

            // Assert
            Assert.Equal(expected, actual, ColorSpaceComparer);

            for (int i = 0; i < actualSpan.Length; i++)
            {
                Assert.Equal(expected, actualSpan[i], ColorSpaceComparer);
            }
        }

        /// <summary>
        /// Tests conversion from <see cref="CieXyz"/> to <see cref="Cmyk"/>.
        /// </summary>
        [Theory]
        [InlineData(0.9504699, 1, 1.08883, 1.192093E-07, 0, 0, 5.960464E-08)]
        [InlineData(0.2139058, 0.3118104, 0.0637231, 0.2865805, 0, 0.7975187, 0.3498302)]
        public void Convert_CieXyz_to_Cmyk(float x, float y2, float z, float c, float m, float y, float k)
        {
            // Arrange
            var input = new CieXyz(x, y2, z);
            var expected = new Cmyk(c, m, y, k);

            Span<CieXyz> inputSpan = new CieXyz[5];
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