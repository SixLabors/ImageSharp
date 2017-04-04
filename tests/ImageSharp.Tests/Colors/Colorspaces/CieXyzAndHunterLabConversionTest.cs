namespace ImageSharp.Tests
{
    using System.Collections.Generic;
    using ImageSharp.Colors.Spaces;
    using ImageSharp.Colors.Spaces.Conversion;

    using Xunit;

    /// <summary>
    /// Tests <see cref="CieXyz"/>-<see cref="CieLab"/> conversions.
    /// </summary>
    /// <remarks>
    /// Test data generated using:
    /// http://www.brucelindbloom.com/index.html?ColorCalculator.html
    /// </remarks>
    public class CieXyzAndHunterLabConversionTest
    {
        private static readonly IEqualityComparer<float> FloatComparer = new ApproximateFloatComparer(4);

        /// <summary>
        /// Tests conversion from <see cref="CieLab"/> to <see cref="CieXyz"/> (<see cref="Illuminants.C"/>).
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(100, 0, 0, 0.98074, 1, 1.18232)] // C white point is HunterLab 100, 0, 0
        public void Convert_HunterLab_to_XYZ(float l, float a, float b, float x, float y, float z)
        {
            // Arrange
            HunterLab input = new HunterLab(l, a, b);
            ColorSpaceConverter converter = new ColorSpaceConverter { WhitePoint = Illuminants.C };

            // Act
            CieXyz output = converter.ToCieXyz(input);

            // Assert
            Assert.Equal(output.X, x, FloatComparer);
            Assert.Equal(output.Y, y, FloatComparer);
            Assert.Equal(output.Z, z, FloatComparer);
        }

        /// <summary>
        /// Tests conversion from <see cref="CieLab"/> to <see cref="CieXyz"/> (<see cref="Illuminants.D65"/>).
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(100, 0, 0, 0.95047, 1, 1.08883)] // D65 white point is HunerLab 100, 0, 0 (adaptation to C performed)
        public void Convert_HunterLab_to_XYZ_D65(float l, float a, float b, float x, float y, float z)
        {
            // Arrange
            HunterLab input = new HunterLab(l, a, b);
            ColorSpaceConverter converter = new ColorSpaceConverter { WhitePoint = Illuminants.D65 };

            // Act
            CieXyz output = converter.ToCieXyz(input);

            // Assert
            Assert.Equal(output.X, x, FloatComparer);
            Assert.Equal(output.Y, y, FloatComparer);
            Assert.Equal(output.Z, z, FloatComparer);
        }

        /// <summary>
        /// Tests conversion from <see cref="CieXyz"/> (<see cref="Illuminants.D65"/>) to <see cref="CieLab"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(0.95047, 1, 1.08883, 100, 0, 0)] // D65 white point is HunerLab 100, 0, 0 (adaptation to C performed)
        public void Convert_XYZ_D65_to_HunterLab(float x, float y, float z, float l, float a, float b)
        {
            // Arrange
            CieXyz input = new CieXyz(x, y, z);
            ColorSpaceConverter converter = new ColorSpaceConverter { WhitePoint = Illuminants.D65 };

            // Act
            HunterLab output = converter.ToHunterLab(input);

            // Assert
            Assert.Equal(output.L, l, FloatComparer);
            Assert.Equal(output.A, a, FloatComparer);
            Assert.Equal(output.B, b, FloatComparer);
        }
    }
}