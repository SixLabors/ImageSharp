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
    public class RgbAndCieXyzConversionTest
    {
        private static readonly IEqualityComparer<float> FloatRoundingComparer = new FloatRoundingComparer(5);

        private static readonly ApproximateFloatComparer ApproximateComparer = new ApproximateFloatComparer(0.0001F);

        /// <summary>
        /// Tests conversion from <see cref="CieXyz"/> (<see cref="Illuminants.D50"/>)
        /// to <see cref="Rgb"/> (<see cref="Rgb.DefaultWorkingSpace">default sRGB working space</see>).
        /// </summary>
        [Theory]
        [InlineData(0.96422, 1.00000, 0.82521, 1, 1, 1)]
        [InlineData(0.00000, 1.00000, 0.00000, 0, 1, 0)]
        [InlineData(0.96422, 0.00000, 0.00000, 1, 0, 0.292064)]
        [InlineData(0.00000, 0.00000, 0.82521, 0, 0.181415, 1)]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(0.297676, 0.267854, 0.045504, 0.720315, 0.509999, 0.168112)]
        public void Convert_XYZ_D50_to_SRGB(float x, float y, float z, float r, float g, float b)
        {
            // Arrange
            var input = new CieXyz(x, y, z);
            var converter = new ColorSpaceConverter { WhitePoint = Illuminants.D50, TargetRgbWorkingSpace = RgbWorkingSpaces.SRgb };

            // Act
            Rgb output = converter.ToRgb(input);

            // Assert
            IEqualityComparer<float> comparer = new ApproximateFloatComparer(0.001f);

            Assert.Equal(Rgb.DefaultWorkingSpace, output.WorkingSpace, ApproximateComparer);
            Assert.Equal(r, output.R, comparer);
            Assert.Equal(g, output.G, comparer);
            Assert.Equal(b, output.B, comparer);
        }

        /// <summary>
        /// Tests conversion
        /// from <see cref="CieXyz"/> (<see cref="Illuminants.D65"/>)
        /// to <see cref="Rgb"/> (<see cref="Rgb.DefaultWorkingSpace">default sRGB working space</see>).
        /// </summary>
        [Theory]
        [InlineData(0.950470, 1.000000, 1.088830, 1, 1, 1)]
        [InlineData(0, 1.000000, 0, 0, 1, 0)]
        [InlineData(0.950470, 0, 0, 1, 0, 0.254967)]
        [InlineData(0, 0, 1.088830, 0, 0.235458, 1)]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(0.297676, 0.267854, 0.045504, 0.754903, 0.501961, 0.099998)]
        public void Convert_XYZ_D65_to_SRGB(float x, float y, float z, float r, float g, float b)
        {
            // Arrange
            CieXyz input = new CieXyz(x, y, z);
            ColorSpaceConverter converter = new ColorSpaceConverter { WhitePoint = Illuminants.D65, TargetRgbWorkingSpace = RgbWorkingSpaces.SRgb };

            // Act
            Rgb output = converter.ToRgb(input);

            // Assert
            Assert.Equal(Rgb.DefaultWorkingSpace, output.WorkingSpace, ApproximateComparer);
            Assert.Equal(r, output.R, FloatRoundingComparer);
            Assert.Equal(g, output.G, FloatRoundingComparer);
            Assert.Equal(b, output.B, FloatRoundingComparer);
        }

        /// <summary>
        /// Tests conversion from <see cref="Rgb"/> (<see cref="Rgb.DefaultWorkingSpace">default sRGB working space</see>)
        /// to <see cref="CieXyz"/> (<see cref="Illuminants.D50"/>).
        /// </summary>
        [Theory]
        [InlineData(1, 1, 1, 0.964220, 1.000000, 0.825210)]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(1, 0, 0, 0.436075, 0.222504, 0.013932)]
        [InlineData(0, 1, 0, 0.385065, 0.716879, 0.0971045)]
        [InlineData(0, 0, 1, 0.143080, 0.060617, 0.714173)]
        [InlineData(0.754902, 0.501961, 0.100000, 0.315757, 0.273323, 0.035506)]
        public void Convert_SRGB_to_XYZ_D50(float r, float g, float b, float x, float y, float z)
        {
            // Arrange
            var input = new Rgb(r, g, b);
            var converter = new ColorSpaceConverter { WhitePoint = Illuminants.D50 };

            // Act
            CieXyz output = converter.ToCieXyz(input);

            // Assert
            IEqualityComparer<float> comparer = new ApproximateFloatComparer(0.001f);
            Assert.Equal(x, output.X, comparer);
            Assert.Equal(y, output.Y, comparer);
            Assert.Equal(z, output.Z, comparer);
        }

        /// <summary>
        /// Tests conversion from <see cref="Rgb"/> (<see cref="Rgb.DefaultWorkingSpace">default sRGB working space</see>)
        /// to <see cref="CieXyz"/> (<see cref="Illuminants.D65"/>).
        /// </summary>
        [Theory]
        [InlineData(1, 1, 1, 0.950470, 1.000000, 1.088830)]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(1, 0, 0, 0.412456, 0.212673, 0.019334)]
        [InlineData(0, 1, 0, 0.357576, 0.715152, 0.119192)]
        [InlineData(0, 0, 1, 0.1804375, 0.072175, 0.950304)]
        [InlineData(0.754902, 0.501961, 0.100000, 0.297676, 0.267854, 0.045504)]
        public void Convert_SRGB_to_XYZ_D65(float r, float g, float b, float x, float y, float z)
        {
            // Arrange
            var input = new Rgb(r, g, b);
            var converter = new ColorSpaceConverter { WhitePoint = Illuminants.D65 };

            // Act
            CieXyz output = converter.ToCieXyz(input);

            // Assert
            Assert.Equal(x, output.X, FloatRoundingComparer);
            Assert.Equal(y, output.Y, FloatRoundingComparer);
            Assert.Equal(z, output.Z, FloatRoundingComparer);
        }
    }
}