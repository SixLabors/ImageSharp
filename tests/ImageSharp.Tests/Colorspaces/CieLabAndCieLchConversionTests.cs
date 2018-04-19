// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces
{
    /// <summary>
    /// Tests <see cref="CieLab"/>-<see cref="CieLch"/> conversions.
    /// </summary>
    /// <remarks>
    /// Test data generated using:
    /// <see href="http://www.brucelindbloom.com/index.html?ColorCalculator.html"/>
    /// </remarks>
    public class CieLabAndCieLchConversionTests
    {
        private static readonly IEqualityComparer<float> FloatRoundingComparer = new FloatRoundingComparer(4);

        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        /// <summary>
        /// Tests conversion from <see cref="CieLch"/> to <see cref="CieLab"/>.
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
        public void Convert_Lch_to_Lab(float l, float c, float h, float l2, float a, float b)
        {
            // Arrange
            var input = new CieLch(l, c, h);

            // Act
            var output = Converter.ToCieLab(input);

            // Assert
            Assert.Equal(l2, output.L, FloatRoundingComparer);
            Assert.Equal(a, output.A, FloatRoundingComparer);
            Assert.Equal(b, output.B, FloatRoundingComparer);
        }

        /// <summary>
        /// Tests conversion from <see cref="CieLab"/> to <see cref="CieLch"/>.
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
        public void Convert_Lab_to_LCHab(float l, float a, float b, float l2, float c, float h)
        {
            // Arrange
            var input = new CieLab(l, a, b);

            // Act
            var output = Converter.ToCieLch(input);

            // Assert
            Assert.Equal(l2, output.L, FloatRoundingComparer);
            Assert.Equal(c, output.C, FloatRoundingComparer);
            Assert.Equal(h, output.H, FloatRoundingComparer);
        }
    }
}