// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
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
    public class CieXyzAndHunterLabConversionTest
    {
        private static readonly IEqualityComparer<float> FloatRoundingComparer = new FloatRoundingComparer(4);

        /// <summary>
        /// Tests conversion from <see cref="HunterLab"/> to <see cref="CieXyz"/> (<see cref="Illuminants.C"/>).
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(100, 0, 0, 0.98074, 1, 1.18232)] // C white point is HunterLab 100, 0, 0
        public void Convert_HunterLab_to_Xyz(float l, float a, float b, float x, float y, float z)
        {
            // Arrange
            var input = new HunterLab(l, a, b);
            var converter = new ColorSpaceConverter { WhitePoint = Illuminants.C };

            // Act
            CieXyz output = converter.ToCieXyz(input);

            // Assert
            Assert.Equal(x, output.X, FloatRoundingComparer);
            Assert.Equal(y, output.Y, FloatRoundingComparer);
            Assert.Equal(z, output.Z, FloatRoundingComparer);
        }

        /// <summary>
        /// Tests conversion from <see cref="HunterLab"/> to <see cref="CieXyz"/> (<see cref="Illuminants.D65"/>).
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(100, 0, 0, 0.95047, 1, 1.08883)] // D65 white point is HunerLab 100, 0, 0 (adaptation to C performed)
        public void Convert_HunterLab_to_Xyz_D65(float l, float a, float b, float x, float y, float z)
        {
            // Arrange
            var input = new HunterLab(l, a, b);
            var converter = new ColorSpaceConverter { WhitePoint = Illuminants.D65 };

            // Act
            CieXyz output = converter.ToCieXyz(input);

            // Assert
            Assert.Equal(x, output.X, FloatRoundingComparer);
            Assert.Equal(y, output.Y, FloatRoundingComparer);
            Assert.Equal(z, output.Z, FloatRoundingComparer);
        }

        /// <summary>
        /// Tests conversion from <see cref="CieXyz"/> (<see cref="Illuminants.D65"/>) to <see cref="HunterLab"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(0.95047, 1, 1.08883, 100, 0, 0)] // D65 white point is HunterLab 100, 0, 0 (adaptation to C performed)
        public void Convert_Xyz_D65_to_HunterLab(float x, float y, float z, float l, float a, float b)
        {
            // Arrange
            var input = new CieXyz(x, y, z);
            var converter = new ColorSpaceConverter { WhitePoint = Illuminants.D65 };

            // Act
            HunterLab output = converter.ToHunterLab(input);

            // Assert
            Assert.Equal(l, output.L, FloatRoundingComparer);
            Assert.Equal(a, output.A, FloatRoundingComparer);
            Assert.Equal(b, output.B, FloatRoundingComparer);
        }
    }
}