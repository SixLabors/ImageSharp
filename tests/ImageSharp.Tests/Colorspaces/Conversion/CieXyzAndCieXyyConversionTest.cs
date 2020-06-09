// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieXyz"/>-<see cref="CieXyy"/> conversions.
    /// </summary>
    /// <remarks>
    /// Test data generated using:
    /// <see href="http://www.brucelindbloom.com/index.html?ColorCalculator.html"/>
    /// </remarks>
    public class CieXyzAndCieXyyConversionTest
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0001F);
        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        [Theory]
        [InlineData(0.436075, 0.222504, 0.013932, 0.648427, 0.330856, 0.222504)]
        [InlineData(0.964220, 1.000000, 0.825210, 0.345669, 0.358496, 1.000000)]
        [InlineData(0.434119, 0.356820, 0.369447, 0.374116, 0.307501, 0.356820)]
        [InlineData(0, 0, 0, 0.538842, 0.000000, 0.000000)]
        public void Convert_xyY_to_XYZ(float xyzX, float xyzY, float xyzZ, float x, float y, float yl)
        {
            var input = new CieXyy(x, y, yl);
            var expected = new CieXyz(xyzX, xyzY, xyzZ);

            Span<CieXyy> inputSpan = new CieXyy[5];
            inputSpan.Fill(input);

            Span<CieXyz> actualSpan = new CieXyz[5];

            // Act
            var actual = Converter.ToCieXyz(input);
            Converter.Convert(inputSpan, actualSpan);

            // Assert
            Assert.Equal(expected, actual, ColorSpaceComparer);

            for (int i = 0; i < actualSpan.Length; i++)
            {
                Assert.Equal(expected, actualSpan[i], ColorSpaceComparer);
            }
        }

        [Theory]
        [InlineData(0.436075, 0.222504, 0.013932, 0.648427, 0.330856, 0.222504)]
        [InlineData(0.964220, 1.000000, 0.825210, 0.345669, 0.358496, 1.000000)]
        [InlineData(0.434119, 0.356820, 0.369447, 0.374116, 0.307501, 0.356820)]
        [InlineData(0.231809, 0, 0.077528, 0.749374, 0.000000, 0.000000)]
        public void Convert_XYZ_to_xyY(float xyzX, float xyzY, float xyzZ, float x, float y, float yl)
        {
            var input = new CieXyz(xyzX, xyzY, xyzZ);
            var expected = new CieXyy(x, y, yl);

            Span<CieXyz> inputSpan = new CieXyz[5];
            inputSpan.Fill(input);

            Span<CieXyy> actualSpan = new CieXyy[5];

            // Act
            var actual = Converter.ToCieXyy(input);
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
