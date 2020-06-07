// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieXyy"/>-<see cref="Lms"/> conversions.
    /// </summary>
    public class CieXyyAndLmsConversionTests
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0002F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="CieXyy"/> to <see cref="Lms"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(0.360555, 0.936901, 0.1001514, 0.06631134, 0.1415282, -0.03809926)]
        public void Convert_CieXyy_to_Lms(float x, float y, float yl, float l, float m, float s)
        {
            // Arrange
            var input = new CieXyy(x, y, yl);
            var expected = new Lms(l, m, s);

            Span<CieXyy> inputSpan = new CieXyy[5];
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
        /// Tests conversion from <see cref="Lms"/> to <see cref="CieXyy"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(0.06631134, 0.1415282, -0.03809926, 0.360555, 0.9369009, 0.1001514)]
        public void Convert_Lms_to_CieXyy(float l, float m, float s, float x, float y, float yl)
        {
            // Arrange
            var input = new Lms(l, m, s);
            var expected = new CieXyy(x, y, yl);

            Span<Lms> inputSpan = new Lms[5];
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
