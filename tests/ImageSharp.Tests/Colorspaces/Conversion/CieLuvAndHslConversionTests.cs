// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieLuv"/>-<see cref="Hsl"/> conversions.
    /// </summary>
    public class CieLuvAndHslConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="CieLuv"/> to <see cref="Hsl"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(36.0555, 93.6901, 10.01514, 347.3767, 0.7115612, 0.3765343)]
        public void Convert_CieLuv_to_Hsl(float l, float u, float v, float h, float s, float l2)
        {
            // Arrange
            var input = new CieLuv(l, u, v);
            var expected = new Hsl(h, s, l2);

            Span<CieLuv> inputSpan = new CieLuv[5];
            inputSpan.Fill(input);

            Span<Hsl> actualSpan = new Hsl[5];

            // Act
            var actual = Converter.ToHsl(input);
            Converter.Convert(inputSpan, actualSpan);

            // Assert
            Assert.Equal(expected, actual, ColorSpaceComparer);

            for (int i = 0; i < actualSpan.Length; i++)
            {
                Assert.Equal(expected, actualSpan[i], ColorSpaceComparer);
            }
        }

        /// <summary>
        /// Tests conversion from <see cref="Hsl"/> to <see cref="CieLuv"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(347.3767, 0.7115612, 0.3765343, 36.0555, 93.69012, 10.01514)]
        public void Convert_Hsl_to_CieLuv(float h, float s, float l2, float l, float u, float v)
        {
            // Arrange
            var input = new Hsl(h, s, l2);
            var expected = new CieLuv(l, u, v);

            Span<Hsl> inputSpan = new Hsl[5];
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
