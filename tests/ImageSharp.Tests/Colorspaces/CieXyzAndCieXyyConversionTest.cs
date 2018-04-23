// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces
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
        private static readonly IEqualityComparer<float> FloatRoundingComparer = new FloatRoundingComparer(4);

        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        [Theory]
        [InlineData(0.436075, 0.222504, 0.013932, 0.648427, 0.330856, 0.222504)]
        [InlineData(0.964220, 1.000000, 0.825210, 0.345669, 0.358496, 1.000000)]
        [InlineData(0.434119, 0.356820, 0.369447, 0.374116, 0.307501, 0.356820)]
        [InlineData(0, 0, 0, 0.538842, 0.000000, 0.000000)]
        public void Convert_xyY_to_XYZ(float xyzX, float xyzY, float xyzZ, float x, float y, float yl)
        {
            var input = new CieXyy(x, y, yl);

            // Act
            CieXyz output = Converter.ToCieXyz(input);

            // Assert
            Assert.Equal(xyzX, output.X, FloatRoundingComparer);
            Assert.Equal(xyzY, output.Y, FloatRoundingComparer);
            Assert.Equal(xyzZ, output.Z, FloatRoundingComparer);
        }

        [Theory]
        [InlineData(0.436075, 0.222504, 0.013932, 0.648427, 0.330856, 0.222504)]
        [InlineData(0.964220, 1.000000, 0.825210, 0.345669, 0.358496, 1.000000)]
        [InlineData(0.434119, 0.356820, 0.369447, 0.374116, 0.307501, 0.356820)]
        [InlineData(0.231809, 0, 0.077528, 0.749374, 0.000000, 0.000000)]
        public void Convert_XYZ_to_xyY(float xyzX, float xyzY, float xyzZ, float x, float y, float yl)
        {
            var input = new CieXyz(xyzX, xyzY, xyzZ);

            // Act
            CieXyy output = Converter.ToCieXyy(input);

            // Assert
            Assert.Equal(x, output.X, FloatRoundingComparer);
            Assert.Equal(y, output.Y, FloatRoundingComparer);
            Assert.Equal(yl, output.Yl, FloatRoundingComparer);
        }
    }
}
