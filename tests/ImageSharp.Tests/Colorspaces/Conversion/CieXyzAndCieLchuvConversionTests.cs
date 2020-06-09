// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieXyz"/>-<see cref="CieLchuv"/> conversions.
    /// </summary>
    public class CieXyzAndCieLchuvConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="CieXyz"/> to <see cref="CieLchuv"/>.
        /// </summary>
        [Theory]
        [InlineData(0.360555, 0.936901, 0.1001514, 97.50697, 183.3831, 133.6321)]
        public void Convert_CieXyz_to_CieLchuv(float x, float y, float yl, float l, float c, float h)
        {
            // Arrange
            var input = new CieXyz(x, y, yl);
            var expected = new CieLchuv(l, c, h);

            Span<CieXyz> inputSpan = new CieXyz[5];
            inputSpan.Fill(input);

            Span<CieLchuv> actualSpan = new CieLchuv[5];

            // Act
            var actual = Converter.ToCieLchuv(input);
            Converter.Convert(inputSpan, actualSpan);

            // Assert
            Assert.Equal(expected, actual, ColorSpaceComparer);

            for (int i = 0; i < actualSpan.Length; i++)
            {
                Assert.Equal(expected, actualSpan[i], ColorSpaceComparer);
            }
        }

        /// <summary>
        /// Tests conversion from <see cref="CieLchuv"/> to <see cref="CieXyz"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(97.50697, 183.3831, 133.6321, 0.360555, 0.936901, 0.1001515)]
        public void Convert_CieLchuv_to_CieXyz(float l, float c, float h, float x, float y, float yl)
        {
            // Arrange
            var input = new CieLchuv(l, c, h);
            var expected = new CieXyz(x, y, yl);

            Span<CieLchuv> inputSpan = new CieLchuv[5];
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
