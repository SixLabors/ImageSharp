﻿// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces
{
    /// <summary>
    /// Tests <see cref="CieXyz"/>-<see cref="CieLab"/> conversions.
    /// </summary>
    /// <remarks>
    /// Test data generated using:
    /// <see href="http://www.brucelindbloom.com/index.html?ColorCalculator.html"/>
    /// </remarks>
    public class CieXyzAndCieLabConversionTest
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0001F);

        /// <summary>
        /// Tests conversion from <see cref="CieLab"/> to <see cref="CieXyz"/> (<see cref="Illuminants.D65"/>).
        /// </summary>
        [Theory]
        [InlineData(100, 0, 0, 0.95047, 1, 1.08883)]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(0, 431.0345, 0, 0.95047, 0, 0)]
        [InlineData(100, -431.0345, 172.4138, 0, 1, 0)]
        [InlineData(0, 0, -172.4138, 0, 0, 1.08883)]
        [InlineData(45.6398, 39.8753, 35.2091, 0.216938, 0.150041, 0.048850)]
        [InlineData(77.1234, -40.1235, 78.1120, 0.358530, 0.517372, 0.076273)]
        [InlineData(10, -400, 20, 0, 0.011260, 0)]
        public void Convert_Lab_to_Xyz(float l, float a, float b, float x, float y, float z)
        {
            // Arrange
            var input = new CieLab(l, a, b, Illuminants.D65);
            var converter = new ColorSpaceConverter { WhitePoint = Illuminants.D65, TargetLabWhitePoint = Illuminants.D65 };
            var expected = new CieXyz(x, y, z);

            // Act
            var actual = converter.ToCieXyz(input);

            // Assert
            Assert.Equal(expected, actual, ColorSpaceComparer);
        }

        /// <summary>
        /// Tests conversion from <see cref="CieXyz"/> (<see cref="Illuminants.D65"/>) to <see cref="CieLab"/>.
        /// </summary>
        [Theory]
        [InlineData(0.95047, 1, 1.08883, 100, 0, 0)]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(0.95047, 0, 0, 0, 431.0345, 0)]
        [InlineData(0, 1, 0, 100, -431.0345, 172.4138)]
        [InlineData(0, 0, 1.08883, 0, 0, -172.4138)]
        [InlineData(0.216938, 0.150041, 0.048850, 45.6398, 39.8753, 35.2091)]
        public void Convert_Xyz_to_Lab(float x, float y, float z, float l, float a, float b)
        {
            // Arrange
            var input = new CieXyz(x, y, z);
            var converter = new ColorSpaceConverter { WhitePoint = Illuminants.D65, TargetLabWhitePoint = Illuminants.D65 };
            var expected = new CieLab(l, a, b);

            // Act
            var actual = converter.ToCieLab(input);

            // Assert
            Assert.Equal(expected, actual, ColorSpaceComparer);
        }
    }
}