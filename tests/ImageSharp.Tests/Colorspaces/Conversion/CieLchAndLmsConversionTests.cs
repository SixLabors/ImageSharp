// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieLch"/>-<see cref="Lms"/> conversions.
    /// </summary>
    public class CieLchAndLmsConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="CieLch"/> to <see cref="Lms"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(36.0555, 103.6901, 10.01514, 0.2440057, -0.04603009, 0.05780027)]
        public void Convert_CieLch_to_Lms(float l, float c, float h, float l2, float m, float s)
        {
            // Arrange
            var input = new CieLch(l, c, h);
            var expected = new Lms(l2, m, s);

            Span<CieLch> inputSpan = new CieLch[5];
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

        /// <summary>
        /// Tests conversion from <see cref="Lms"/> to <see cref="CieLch"/>.
        /// </summary>
        [Theory]
        [InlineData(0.2440057, -0.04603009, 0.05780027, 36.05552, 103.6901, 10.01515)]
        public void Convert_Lms_to_CieLch(float l2, float m, float s, float l, float c, float h)
        {
            // Arrange
            var input = new Lms(l2, m, s);
            var expected = new CieLch(l, c, h);

            Span<Lms> inputSpan = new Lms[5];
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