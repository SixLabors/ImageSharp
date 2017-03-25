namespace ImageSharp.Tests
{
    using ImageSharp.Colors.Conversion;
    using System.Collections.Generic;
    using ImageSharp.Colors.Spaces;
    using Xunit;

    /// <summary>
    /// Tests <see cref="CieXyz"/>-<see cref="CieLab"/> conversions.
    /// </summary>
    /// <remarks>
    /// Test data generated using:
    /// http://www.brucelindbloom.com/index.html?ColorCalculator.html
    /// </remarks>
    public class CieXyzAndCieLabConversionTest
    {
        private static readonly IEqualityComparer<float> FloatComparerLabPrecision = new ApproximateFloatComparer(4);
        private static readonly IEqualityComparer<float> FloatComparerXyzPrecision = new ApproximateFloatComparer(6);

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
        public void Convert_Lab_to_XYZ(float l, float a, float b, float x, float y, float z)
        {
            // Arrange
            CieLab input = new CieLab(l, a, b, Illuminants.D65);
            ColorSpaceConverter converter = new ColorSpaceConverter { WhitePoint = Illuminants.D65, TargetLabWhitePoint = Illuminants.D65 };

            // Act
            CieXyz output = converter.ToCieXyz(input);

            // Assert
            Assert.Equal(output.X, x, FloatComparerXyzPrecision);
            Assert.Equal(output.Y, y, FloatComparerXyzPrecision);
            Assert.Equal(output.Z, z, FloatComparerXyzPrecision);
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
        public void Convert_XYZ_to_Lab(float x, float y, float z, float l, float a, float b)
        {
            // Arrange
            CieXyz input = new CieXyz(x, y, z);
            ColorSpaceConverter converter = new ColorSpaceConverter { WhitePoint = Illuminants.D65, TargetLabWhitePoint = Illuminants.D65 };

            // Act
            CieLab output = converter.ToCieLab(input);

            // Assert
            Assert.Equal(output.L, l, FloatComparerLabPrecision);
            Assert.Equal(output.A, a, FloatComparerLabPrecision);
            Assert.Equal(output.B, b, FloatComparerLabPrecision);
        }
    }
}