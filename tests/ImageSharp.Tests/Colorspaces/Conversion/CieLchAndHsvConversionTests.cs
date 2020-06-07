// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieLch"/>-<see cref="Hsv"/> conversions.
    /// </summary>
    public class CieLchAndHsvConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="CieLch"/> to <see cref="Hsv"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(36.0555, 103.6901, 10.01514, 341.959, 1, 0.8414602)]
        public void Convert_CieLch_to_Hsv(float l, float c, float h, float h2, float s, float v)
        {
            // Arrange
            var input = new CieLch(l, c, h);
            var expected = new Hsv(h2, s, v);

            Span<CieLch> inputSpan = new CieLch[5];
            inputSpan.Fill(input);

            Span<Hsv> actualSpan = new Hsv[5];

            // Act
            var actual = Converter.ToHsv(input);
            Converter.Convert(inputSpan, actualSpan);

            // Assert
            Assert.Equal(expected, actual, ColorSpaceComparer);

            for (int i = 0; i < actualSpan.Length; i++)
            {
                Assert.Equal(expected, actualSpan[i], ColorSpaceComparer);
            }
        }

        /// <summary>
        /// Tests conversion from <see cref="Hsv"/> to <see cref="CieLch"/>.
        /// </summary>
        [Theory]
        [InlineData(341.959, 1, 0.8414602, 46.13444, 78.0637, 22.90501)]
        public void Convert_Hsv_to_CieLch(float h2, float s, float v, float l, float c, float h)
        {
            // Arrange
            var input = new Hsv(h2, s, v);
            var expected = new CieLch(l, c, h);

            Span<Hsv> inputSpan = new Hsv[5];
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