// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieXyz"/>-<see cref="CieLch"/> conversions.
    /// </summary>
    public class CieXyzAndCieLchConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="CieXyz"/> to <see cref="CieLch"/>.
        /// </summary>
        [Theory]
        [InlineData(0.360555, 0.936901, 0.1001514, 97.50815, 155.8035, 139.323)]
        public void Convert_CieXyz_to_CieLch(float x, float y, float yl, float l, float c, float h)
        {
            // Arrange
            var input = new CieXyz(x, y, yl);
            var expected = new CieLch(l, c, h);

            Span<CieXyz> inputSpan = new CieXyz[5];
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

        /// <summary>
        /// Tests conversion from <see cref="CieLch"/> to <see cref="CieXyz"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(97.50815, 155.8035, 139.323, 0.3605551, 0.936901, 0.1001514)]
        public void Convert_CieLch_to_CieXyz(float l, float c, float h, float x, float y, float yl)
        {
            // Arrange
            var input = new CieLch(l, c, h);
            var expected = new CieXyz(x, y, yl);

            Span<CieLch> inputSpan = new CieLch[5];
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
