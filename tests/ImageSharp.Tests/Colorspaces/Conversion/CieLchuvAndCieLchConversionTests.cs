// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieLchuv"/>-<see cref="CieLch"/> conversions.
    /// </summary>
    public class CieLchuvAndCieLchConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="CieLch"/> to <see cref="CieLchuv"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(36.73742, 64.79149, 30.1786, 36.0555, 103.6901, 10.01513)]
        public void Convert_CieLch_to_CieLchuv(float l2, float c2, float h2, float l, float c, float h)
        {
            // Arrange
            var input = new CieLch(l2, c2, h2);
            var expected = new CieLchuv(l, c, h);

            Span<CieLch> inputSpan = new CieLch[5];
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
        /// Tests conversion from <see cref="CieLchuv"/> to <see cref="CieLch"/>.
        /// </summary>
        [Theory]
        [InlineData(36.0555, 103.6901, 10.01514, 36.73742, 64.79149, 30.1786)]
        public void Convert_CieLchuv_to_CieLch(float l, float c, float h, float l2, float c2, float h2)
        {
            // Arrange
            var input = new CieLchuv(l, c, h);
            var expected = new CieLch(l2, c2, h2);

            Span<CieLchuv> inputSpan = new CieLchuv[5];
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