// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieLab"/>-<see cref="HunterLab"/> conversions.
    /// </summary>
    public class CieLabAndHunterLabConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="HunterLab"/> to <see cref="CieLab"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(27.51646, 556.9392, -0.03974226, 36.05554, 275.6227, 10.01519)]
        public void Convert_HunterLab_to_CieLab(float l2, float a2, float b2, float l, float a, float b)
        {
            // Arrange
            var input = new HunterLab(l2, a2, b2);
            var expected = new CieLab(l, a, b);

            Span<HunterLab> inputSpan = new HunterLab[5];
            inputSpan.Fill(input);

            Span<CieLab> actualSpan = new CieLab[5];

            // Act
            var actual = Converter.ToCieLab(input);
            Converter.Convert(inputSpan, actualSpan);

            // Assert
            Assert.Equal(expected, actual, ColorSpaceComparer);

            for (int i = 0; i < actualSpan.Length; i++)
            {
                Assert.Equal(expected, actualSpan[i], ColorSpaceComparer);
            }
        }

        /// <summary>
        /// Tests conversion from <see cref="CieLab"/> to <see cref="HunterLab"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(36.0555, 303.6901, 10.01514, 27.51646, 556.9392, -0.03974226)]
        public void Convert_CieLab_to_HunterLab(float l, float a, float b, float l2, float a2, float b2)
        {
            // Arrange
            var input = new CieLab(l, a, b);
            var expected = new HunterLab(l2, a2, b2);

            Span<CieLab> inputSpan = new CieLab[5];
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
    }
}