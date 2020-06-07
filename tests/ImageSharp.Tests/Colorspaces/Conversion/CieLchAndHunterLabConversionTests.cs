// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieLch"/>-<see cref="HunterLab"/> conversions.
    /// </summary>
    public class CieLchAndHunterLabConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="CieLch"/> to <see cref="HunterLab"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(36.0555, 103.6901, 10.01514, 29.41358, 106.6302, 9.102425)]
        public void Convert_CieLch_to_HunterLab(float l, float c, float h, float l2, float a, float b)
        {
            // Arrange
            var input = new CieLch(l, c, h);
            var expected = new HunterLab(l2, a, b);

            Span<CieLch> inputSpan = new CieLch[5];
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
        /// Tests conversion from <see cref="HunterLab"/> to <see cref="CieLch"/>.
        /// </summary>
        [Theory]
        [InlineData(29.41358, 106.6302, 9.102425, 36.05551, 103.6901, 10.01515)]
        public void Convert_HunterLab_to_CieLch(float l2, float a, float b, float l, float c, float h)
        {
            // Arrange
            var input = new HunterLab(l2, a, b);
            var expected = new CieLch(l, c, h);

            Span<HunterLab> inputSpan = new HunterLab[5];
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
    }
}