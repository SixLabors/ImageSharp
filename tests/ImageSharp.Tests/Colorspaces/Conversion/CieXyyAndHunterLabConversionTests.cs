// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieXyy"/>-<see cref="HunterLab"/> conversions.
    /// </summary>
    public class CieXyyAndHunterLabConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="CieXyy"/> to <see cref="HunterLab"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(0.360555, 0.936901, 0.1001514, 31.46263, -32.81796, 28.64938)]
        public void Convert_CieXyy_to_HunterLab(float x, float y, float yl, float l, float a, float b)
        {
            // Arrange
            var input = new CieXyy(x, y, yl);
            var expected = new HunterLab(l, a, b);

            Span<CieXyy> inputSpan = new CieXyy[5];
            inputSpan.Fill(input);

            Span<HunterLab> actualSpan = new HunterLab[5];

            // Act
            var actual = Converter.ToHunterLab(input);
            Converter.Convert(inputSpan, actualSpan);

            // Assert
            Assert.Equal(expected, actual, ColorSpaceComparer);

            for (int i = 0; i < actualSpan.Length; i++)
            {
                Assert.Equal(expected, actualSpan[i], ColorSpaceComparer);
            }
        }

        /// <summary>
        /// Tests conversion from <see cref="HunterLab"/> to <see cref="CieXyy"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(31.46263, -32.81796, 28.64938, 0.3605552, 0.9369011, 0.1001514)]
        public void Convert_HunterLab_to_CieXyy(float l, float a, float b, float x, float y, float yl)
        {
            // Arrange
            var input = new HunterLab(l, a, b);
            var expected = new CieXyy(x, y, yl);

            Span<HunterLab> inputSpan = new HunterLab[5];
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
