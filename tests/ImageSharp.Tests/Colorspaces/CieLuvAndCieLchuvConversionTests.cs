// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces
{
    /// <summary>
    /// Tests <see cref="CieLuv"/>-<see cref="CieLchuv"/> conversions.
    /// </summary>
    /// <remarks>
    /// Test data generated using:
    /// <see href="http://www.brucelindbloom.com/index.html?ColorCalculator.html"/>
    /// </remarks>
    public class CieLuvAndCieLchuvuvConversionTests
    {
        private static readonly IEqualityComparer<float> FloatRoundingComparer = new FloatRoundingComparer(4);

        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="CieLchuv"/> to <see cref="CieLuv"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(54.2917, 106.8391, 40.8526, 54.2917, 80.8125, 69.8851)]
        [InlineData(100, 0, 0, 100, 0, 0)]
        [InlineData(100, 50, 180, 100, -50, 0)]
        [InlineData(10, 36.0555, 56.3099, 10, 20, 30)]
        [InlineData(10, 36.0555, 123.6901, 10, -20, 30)]
        [InlineData(10, 36.0555, 303.6901, 10, 20, -30)]
        [InlineData(10, 36.0555, 236.3099, 10, -20, -30)]
        public void Convert_Lchuv_to_Luv(float l, float c, float h, float l2, float u, float v)
        {
            // Arrange
            var input = new CieLchuv(l, c, h);

            // Act
            CieLuv output = Converter.ToCieLuv(input);

            // Assert
            Assert.Equal(l2, output.L, FloatRoundingComparer);
            Assert.Equal(u, output.U, FloatRoundingComparer);
            Assert.Equal(v, output.V, FloatRoundingComparer);
        }

        /// <summary>
        /// Tests conversion from <see cref="CieLuv"/> to <see cref="CieLchuv"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(54.2917, 80.8125, 69.8851, 54.2917, 106.8391, 40.8526)]
        [InlineData(100, 0, 0, 100, 0, 0)]
        [InlineData(100, -50, 0, 100, 50, 180)]
        [InlineData(10, 20, 30, 10, 36.0555, 56.3099)]
        [InlineData(10, -20, 30, 10, 36.0555, 123.6901)]
        [InlineData(10, 20, -30, 10, 36.0555, 303.6901)]
        [InlineData(10, -20, -30, 10, 36.0555, 236.3099)]
        [InlineData(37.3511, 24.1720, 16.0684, 37.3511, 29.0255, 33.6141)]
        public void Convert_Luv_to_LCHuv(float l, float u, float v, float l2, float c, float h)
        {
            // Arrange
            var input = new CieLuv(l, u, v);

            // Act
            CieLchuv output = Converter.ToCieLchuv(input);

            // Assert
            Assert.Equal(l2, output.L, FloatRoundingComparer);
            Assert.Equal(c, output.C, FloatRoundingComparer);
            Assert.Equal(h, output.H, FloatRoundingComparer);
        }
    }
}