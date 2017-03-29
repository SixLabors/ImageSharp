namespace ImageSharp.Tests
{
    using System.Collections.Generic;

    using ImageSharp.Colors.Spaces;
    using ImageSharp.Colors.Spaces.Conversion;
    using ImageSharp.Colors.Spaces.Conversion.Implementation.Lms;

    using Xunit;

    public class ColorConverterAdaptTest
    {
        private static readonly IEqualityComparer<float> FloatComparer = new ApproximateFloatComparer(4);

        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(1, 1, 1, 1, 1, 1)]
        [InlineData(0.206162, 0.260277, 0.746717, 0.220000, 0.130000, 0.780000)]
        public void Adapt_RGB_WideGamutRGB_To_sRGB(float r1, float g1, float b1, float r2, float g2, float b2)
        {
            // Arrange
            Rgb input = new Rgb(r1, g1, b1, RgbWorkingSpaces.WideGamutRgb);
            Rgb expectedOutput = new Rgb(r2, g2, b2, RgbWorkingSpaces.SRgb);
            ColorSpaceConverter converter = new ColorSpaceConverter { TargetRgbWorkingSpace = RgbWorkingSpaces.SRgb };

            // Action
            Rgb output = converter.Adapt(input);

            // Assert
            Assert.Equal(expectedOutput.WorkingSpace, output.WorkingSpace);
            Assert.Equal(output.R, expectedOutput.R, FloatComparer);
            Assert.Equal(output.G, expectedOutput.G, FloatComparer);
            Assert.Equal(output.B, expectedOutput.B, FloatComparer);
        }

        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(1, 1, 1, 1, 1, 1)]
        [InlineData(0.220000, 0.130000, 0.780000, 0.206162, 0.260277, 0.746717)]
        public void Adapt_RGB_SRGB_To_WideGamutRGB(float r1, float g1, float b1, float r2, float g2, float b2)
        {
            // Arrange
            Rgb input = new Rgb(r1, g1, b1, RgbWorkingSpaces.SRgb);
            Rgb expectedOutput = new Rgb(r2, g2, b2, RgbWorkingSpaces.WideGamutRgb);
            ColorSpaceConverter converter = new ColorSpaceConverter { TargetRgbWorkingSpace = RgbWorkingSpaces.WideGamutRgb };

            // Action
            Rgb output = converter.Adapt(input);

            // Assert
            Assert.Equal(expectedOutput.WorkingSpace, output.WorkingSpace);
            Assert.Equal(output.R, expectedOutput.R, FloatComparer);
            Assert.Equal(output.G, expectedOutput.G, FloatComparer);
            Assert.Equal(output.B, expectedOutput.B, FloatComparer);
        }

        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(22, 33, 1, 22.269869, 32.841164, 1.633926)]
        public void Adapt_Lab_D65_To_D50(float l1, float a1, float b1, float l2, float a2, float b2)
        {
            // Arrange
            CieLab input = new CieLab(l1, a1, b1, Illuminants.D65);
            CieLab expectedOutput = new CieLab(l2, a2, b2);
            ColorSpaceConverter converter = new ColorSpaceConverter { TargetLabWhitePoint = Illuminants.D50 };

            // Action
            CieLab output = converter.Adapt(input);

            // Assert
            Assert.Equal(output.L, expectedOutput.L, FloatComparer);
            Assert.Equal(output.A, expectedOutput.A, FloatComparer);
            Assert.Equal(output.B, expectedOutput.B, FloatComparer);
        }

        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(0.5, 0.5, 0.5, 0.510286, 0.501489, 0.378970)]
        public void Adapt_XYZ_D65_To_D50_Bradford(float x1, float y1, float z1, float x2, float y2, float z2)
        {
            // Arrange
            CieXyz input = new CieXyz(x1, y1, z1);
            CieXyz expectedOutput = new CieXyz(x2, y2, z2);
            ColorSpaceConverter converter = new ColorSpaceConverter
            {
                WhitePoint = Illuminants.D50
            };

            // Action
            CieXyz output = converter.Adapt(input, Illuminants.D65);

            // Assert
            Assert.Equal(output.X, expectedOutput.X, FloatComparer);
            Assert.Equal(output.Y, expectedOutput.Y, FloatComparer);
            Assert.Equal(output.Z, expectedOutput.Z, FloatComparer);
        }

        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(0.5, 0.5, 0.5, 0.507233, 0.500000, 0.378943)]
        public void Adapt_CieXyz_D65_To_D50_XyzScaling(float x1, float y1, float z1, float x2, float y2, float z2)
        {
            // Arrange
            CieXyz input = new CieXyz(x1, y1, z1);
            CieXyz expectedOutput = new CieXyz(x2, y2, z2);
            ColorSpaceConverter converter = new ColorSpaceConverter
            {
                ChromaticAdaptation = new VonKriesChromaticAdaptation(LmsAdaptationMatrix.XYZScaling),
                WhitePoint = Illuminants.D50
            };

            // Action
            CieXyz output = converter.Adapt(input, Illuminants.D65);

            // Assert
            Assert.Equal(output.X, expectedOutput.X, FloatComparer);
            Assert.Equal(output.Y, expectedOutput.Y, FloatComparer);
            Assert.Equal(output.Z, expectedOutput.Z, FloatComparer);
        }

        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(0.5, 0.5, 0.5, 0.507233, 0.500000, 0.378943)]
        public void Adapt_XYZ_D65_To_D50_XYZScaling(float x1, float y1, float z1, float x2, float y2, float z2)
        {
            // Arrange
            CieXyz input = new CieXyz(x1, y1, z1);
            CieXyz expectedOutput = new CieXyz(x2, y2, z2);
            ColorSpaceConverter converter = new ColorSpaceConverter
            {
                ChromaticAdaptation = new VonKriesChromaticAdaptation(LmsAdaptationMatrix.XYZScaling),
                WhitePoint = Illuminants.D50
            };

            // Action
            CieXyz output = converter.Adapt(input, Illuminants.D65);

            // Assert
            Assert.Equal(output.X, expectedOutput.X, FloatComparer);
            Assert.Equal(output.Y, expectedOutput.Y, FloatComparer);
            Assert.Equal(output.Z, expectedOutput.Z, FloatComparer);
        }
    }
}