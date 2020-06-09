// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieLab"/>-<see cref="Lms"/> conversions.
    /// </summary>
    public class CieLabAndLmsConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="Lms"/> to <see cref="CieLab"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(0.8303261, -0.5776886, 0.1133359, 36.05553, 275.6228, 10.01518)]
        public void Convert_Lms_to_CieLab(float l2, float m, float s, float l, float a, float b)
        {
            // Arrange
            var input = new Lms(l2, m, s);
            var expected = new CieLab(l, a, b);

            Span<Lms> inputSpan = new Lms[5];
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
        /// Tests conversion from <see cref="CieLab"/> to <see cref="Lms"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(36.0555, 303.6901, 10.01514, 0.8303261, -0.5776886, 0.1133359)]
        public void Convert_CieLab_to_Lms(float l, float a, float b, float l2, float m, float s)
        {
            // Arrange
            var input = new CieLab(l, a, b);
            var expected = new Lms(l2, m, s);

            Span<CieLab> inputSpan = new CieLab[5];
            inputSpan.Fill(input);

            Span<Lms> actualSpan = new Lms[5];

            // Act
            var actual = Converter.ToLms(input);
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