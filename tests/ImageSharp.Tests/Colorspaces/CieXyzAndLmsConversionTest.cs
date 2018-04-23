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
    /// Test data generated using original colorful library.
    /// </remarks>
    public class CieXyzAndLmsConversionTest
    {
        private static readonly IEqualityComparer<float> FloatRoundingComparer = new FloatRoundingComparer(5);

        /// <summary>
        /// Tests conversion from <see cref="CieXyz"/> (<see cref="Illuminants.D65"/>) to <see cref="Lms"/>.
        /// </summary>
        [Theory]
        [InlineData(0.941428535, 1.040417467, 1.089532651, 0.95047, 1, 1.08883)]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(0.850765697, -0.713042594, 0.036973283, 0.95047, 0, 0)]
        [InlineData(0.2664, 1.7135, -0.0685, 0, 1, 0)]
        [InlineData(-0.175737162, 0.039960061, 1.121059368, 0, 0, 1.08883)]
        [InlineData(0.2262677362, 0.0961411609, 0.0484570397, 0.216938, 0.150041, 0.048850)]
        public void Convert_Lms_to_CieXyz(float l, float m, float s, float x, float y, float z)
        {
            // Arrange
            var input = new Lms(l, m, s);
            var converter = new ColorSpaceConverter();

            // Act
            CieXyz output = converter.ToCieXyz(input);

            // Assert
            Assert.Equal(x, output.X, FloatRoundingComparer);
            Assert.Equal(y, output.Y, FloatRoundingComparer);
            Assert.Equal(z, output.Z, FloatRoundingComparer);
        }

        /// <summary>
        /// Tests conversion from <see cref="CieXyz"/> (<see cref="Illuminants.D65"/>) to <see cref="Lms"/>.
        /// </summary>
        [Theory]
        [InlineData(0.95047, 1, 1.08883, 0.941428535, 1.040417467, 1.089532651)]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(0.95047, 0, 0, 0.850765697, -0.713042594, 0.036973283)]
        [InlineData(0, 1, 0, 0.2664, 1.7135, -0.0685)]
        [InlineData(0, 0, 1.08883, -0.175737162, 0.039960061, 1.121059368)]
        [InlineData(0.216938, 0.150041, 0.048850, 0.2262677362, 0.0961411609, 0.0484570397)]
        public void Convert_CieXyz_to_Lms(float x, float y, float z, float l, float m, float s)
        {
            // Arrange
            var input = new CieXyz(x, y, z);
            var converter = new ColorSpaceConverter();

            // Act
            Lms output = converter.ToLms(input);

            // Assert
            Assert.Equal(l, output.L, FloatRoundingComparer);
            Assert.Equal(m, output.M, FloatRoundingComparer);
            Assert.Equal(s, output.S, FloatRoundingComparer);
        }
    }
}