// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieLuv"/>-<see cref="HunterLab"/> conversions.
    /// </summary>
    public class CieLuvAndHunterLabConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="CieLuv"/> to <see cref="HunterLab"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(36.0555, 93.6901, 10.01514, 30.19531, 46.4312, 11.16259)]
        public void Convert_CieLuv_to_HunterLab(float l, float u, float v, float l2, float a, float b)
        {
            // Arrange
            var input = new CieLuv(l, u, v);
            var expected = new HunterLab(l2, a, b);

            Span<CieLuv> inputSpan = new CieLuv[5];
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
        /// Tests conversion from <see cref="HunterLab"/> to <see cref="CieLuv"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(30.19531, 46.4312, 11.16259, 36.0555, 93.6901, 10.01514)]
        public void Convert_HunterLab_to_CieLuv(float l2, float a, float b, float l, float u, float v)
        {
            // Arrange
            var input = new HunterLab(l2, a, b);
            var expected = new CieLuv(l, u, v);

            Span<HunterLab> inputSpan = new HunterLab[5];
            inputSpan.Fill(input);

            Span<CieLuv> actualSpan = new CieLuv[5];

            // Act
            var actual = Converter.ToCieLuv(input);
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
