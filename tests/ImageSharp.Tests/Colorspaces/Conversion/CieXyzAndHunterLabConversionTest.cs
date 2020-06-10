// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieXyz"/>-<see cref="HunterLab"/> conversions.
    /// </summary>
    /// <remarks>
    /// Test data generated using:
    /// <see href="http://www.brucelindbloom.com/index.html?ColorCalculator.html"/>
    /// </remarks>
    public class CieXyzAndHunterLabConversionTest
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0001F);

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
            var options = new ColorSpaceConverterOptions { WhitePoint = Illuminants.C };
            var converter = new ColorSpaceConverter(options);
            var expected = new CieXyz(x, y, z);

            Span<HunterLab> inputSpan = new HunterLab[5];
            inputSpan.Fill(input);

            Span<CieXyz> actualSpan = new CieXyz[5];

            // Act
            var actual = converter.ToCieXyz(input);
            converter.Convert(inputSpan, actualSpan);

            // Assert
            Assert.Equal(expected, actual, ColorSpaceComparer);

            for (int i = 0; i < actualSpan.Length; i++)
            {
                Assert.Equal(expected, actualSpan[i], ColorSpaceComparer);
            }
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
            var options = new ColorSpaceConverterOptions { WhitePoint = Illuminants.D65 };
            var converter = new ColorSpaceConverter(options);
            var expected = new CieXyz(x, y, z);

            Span<HunterLab> inputSpan = new HunterLab[5];
            inputSpan.Fill(input);

            Span<CieXyz> actualSpan = new CieXyz[5];

            // Act
            var actual = converter.ToCieXyz(input);
            converter.Convert(inputSpan, actualSpan);

            // Assert
            Assert.Equal(expected, actual, ColorSpaceComparer);

            for (int i = 0; i < actualSpan.Length; i++)
            {
                Assert.Equal(expected, actualSpan[i], ColorSpaceComparer);
            }
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
            var options = new ColorSpaceConverterOptions { WhitePoint = Illuminants.D65 };
            var converter = new ColorSpaceConverter(options);
            var expected = new HunterLab(l, a, b);

            Span<CieXyz> inputSpan = new CieXyz[5];
            inputSpan.Fill(input);

            Span<HunterLab> actualSpan = new HunterLab[5];

            // Act
            var actual = converter.ToHunterLab(input);
            converter.Convert(inputSpan, actualSpan);

            // Assert
            Assert.Equal(expected, actual, ColorSpaceComparer);

            for (int i = 0; i < actualSpan.Length; i++)
            {
                Assert.Equal(expected, actualSpan[i], ColorSpaceComparer);
            }
        }
    }
}