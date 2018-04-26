// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using SixLabors.ImageSharp.ColorSpaces.Conversion.Implementation.LmsColorSapce;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces
{
    /// <summary>
    /// Tests <see cref="M:ColorSpaceConverter.Adapt" /> methods.
    /// Test data generated using:
    /// <see cref="http://www.brucelindbloom.com/index.html?ChromAdaptCalc.html"/>
    /// <see cref="http://www.brucelindbloom.com/index.html?ColorCalculator.html"/>
    /// </summary>
    public class ColorConverterAdaptTest
    {
        private static readonly IEqualityComparer<float> FloatRoundingComparer = new FloatRoundingComparer(3);

        private static readonly ApproximateFloatComparer ApproximateComparer = new ApproximateFloatComparer(0.0001F);

        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(1, 1, 1, 1, 1, 1)]
        [InlineData(0.206162, 0.260277, 0.746717, 0.220000, 0.130000, 0.780000)]
        public void Adapt_RGB_WideGamutRGB_To_sRGB(float r1, float g1, float b1, float r2, float g2, float b2)
        {
            // Arrange
            var input = new Rgb(r1, g1, b1, RgbWorkingSpaces.WideGamutRgb);
            var expectedOutput = new Rgb(r2, g2, b2, RgbWorkingSpaces.SRgb);
            var converter = new ColorSpaceConverter { TargetRgbWorkingSpace = RgbWorkingSpaces.SRgb };

            // Action
            Rgb output = converter.Adapt(input);

            // Assert
            Assert.Equal(expectedOutput.WorkingSpace, output.WorkingSpace, ApproximateComparer);
            Assert.Equal(expectedOutput.R, output.R, FloatRoundingComparer);
            Assert.Equal(expectedOutput.G, output.G, FloatRoundingComparer);
            Assert.Equal(expectedOutput.B, output.B, FloatRoundingComparer);
        }

        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(1, 1, 1, 1, 1, 1)]
        [InlineData(0.220000, 0.130000, 0.780000, 0.206162, 0.260277, 0.746717)]
        public void Adapt_RGB_SRGB_To_WideGamutRGB(float r1, float g1, float b1, float r2, float g2, float b2)
        {
            // Arrange
            var input = new Rgb(r1, g1, b1, RgbWorkingSpaces.SRgb);
            var expectedOutput = new Rgb(r2, g2, b2, RgbWorkingSpaces.WideGamutRgb);
            var converter = new ColorSpaceConverter { TargetRgbWorkingSpace = RgbWorkingSpaces.WideGamutRgb };

            // Action
            Rgb output = converter.Adapt(input);

            // Assert
            Assert.Equal(expectedOutput.WorkingSpace, output.WorkingSpace, ApproximateComparer);
            Assert.Equal(expectedOutput.R, output.R, FloatRoundingComparer);
            Assert.Equal(expectedOutput.G, output.G, FloatRoundingComparer);
            Assert.Equal(expectedOutput.B, output.B, FloatRoundingComparer);
        }

        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(22, 33, 1, 22.269869, 32.841164, 1.633926)]
        public void Adapt_Lab_D50_To_D65(float l1, float a1, float b1, float l2, float a2, float b2)
        {
            // Arrange
            var input = new CieLab(l1, a1, b1, Illuminants.D65);
            var expectedOutput = new CieLab(l2, a2, b2);
            var converter = new ColorSpaceConverter { TargetLabWhitePoint = Illuminants.D50 };

            // Action
            CieLab output = converter.Adapt(input);

            // Assert
            Assert.Equal(expectedOutput.L, output.L, FloatRoundingComparer);
            Assert.Equal(expectedOutput.A, output.A, FloatRoundingComparer);
            Assert.Equal(expectedOutput.B, output.B, FloatRoundingComparer);
        }

        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(0.5, 0.5, 0.5, 0.510286, 0.501489, 0.378970)]
        public void Adapt_Xyz_D65_To_D50_Bradford(float x1, float y1, float z1, float x2, float y2, float z2)
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
            Assert.Equal(expectedOutput.X, output.X, FloatRoundingComparer);
            Assert.Equal(expectedOutput.Y, output.Y, FloatRoundingComparer);
            Assert.Equal(expectedOutput.Z, output.Z, FloatRoundingComparer);
        }

        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(0.5, 0.5, 0.5, 0.507233, 0.500000, 0.378943)]
        public void Adapt_CieXyz_D65_To_D50_XyzScaling(float x1, float y1, float z1, float x2, float y2, float z2)
        {
            // Arrange
            var input = new CieXyz(x1, y1, z1);
            var expectedOutput = new CieXyz(x2, y2, z2);
            var converter = new ColorSpaceConverter
            {
                ChromaticAdaptation = new VonKriesChromaticAdaptation(LmsAdaptationMatrix.XyzScaling),
                WhitePoint = Illuminants.D50
            };

            // Action
            CieXyz output = converter.Adapt(input, Illuminants.D65);

            // Assert
            Assert.Equal(expectedOutput.X, output.X, FloatRoundingComparer);
            Assert.Equal(expectedOutput.Y, output.Y, FloatRoundingComparer);
            Assert.Equal(expectedOutput.Z, output.Z, FloatRoundingComparer);
        }

        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(0.5, 0.5, 0.5, 0.507233, 0.500000, 0.378943)]
        public void Adapt_Xyz_D65_To_D50_XyzScaling(float x1, float y1, float z1, float x2, float y2, float z2)
        {
            // Arrange
            var input = new CieXyz(x1, y1, z1);
            var expectedOutput = new CieXyz(x2, y2, z2);
            var converter = new ColorSpaceConverter
            {
                ChromaticAdaptation = new VonKriesChromaticAdaptation(LmsAdaptationMatrix.XyzScaling),
                WhitePoint = Illuminants.D50
            };

            // Action
            CieXyz output = converter.Adapt(input, Illuminants.D65);

            // Assert
            Assert.Equal(expectedOutput.X, output.X, FloatRoundingComparer);
            Assert.Equal(expectedOutput.Y, output.Y, FloatRoundingComparer);
            Assert.Equal(expectedOutput.Z, output.Z, FloatRoundingComparer);
        }
    }
}