// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieXyz"/>-<see cref="CieLuv"/> conversions.
    /// </summary>
    /// <remarks>
    /// Test data generated using:
    /// <see href="http://www.brucelindbloom.com/index.html?ColorCalculator.html"/>
    /// </remarks>
    public class CieXyzAndCieLuvConversionTest
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0001F);

        /// <summary>
        /// Tests conversion from <see cref="CieLuv"/> to <see cref="CieXyz"/> (<see cref="Illuminants.D65"/>).
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(0, 100, 50, 0, 0, 0)]
        [InlineData(0.1, 100, 50, 0.000493, 0.000111, 0)]
        [InlineData(70.0000, 86.3525, 2.8240, 0.569310, 0.407494, 0.365843)]
        [InlineData(10.0000, -1.2345, -10.0000, 0.012191, 0.011260, 0.025939)]
        [InlineData(100, 0, 0, 0.950470, 1.000000, 1.088830)]
        [InlineData(1, 1, 1, 0.001255, 0.001107, 0.000137)]
        public void Convert_Luv_to_Xyz(float l, float u, float v, float x, float y, float z)
        {
            // Arrange
            var input = new CieLuv(l, u, v, Illuminants.D65);
            var options = new ColorSpaceConverterOptions { WhitePoint = Illuminants.D65, TargetLabWhitePoint = Illuminants.D65 };
            var converter = new ColorSpaceConverter(options);
            var expected = new CieXyz(x, y, z);

            Span<CieLuv> inputSpan = new CieLuv[5];
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
        /// Tests conversion from <see cref="CieXyz"/> (<see cref="Illuminants.D65"/>) to <see cref="CieLuv"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(0.000493, 0.000111, 0, 0.1003, 0.9332, -0.0070)]
        [InlineData(0.569310, 0.407494, 0.365843, 70.0000, 86.3524, 2.8240)]
        [InlineData(0.012191, 0.011260, 0.025939, 9.9998, -1.2343, -9.9999)]
        [InlineData(0.950470, 1.000000, 1.088830, 100, 0, 0)]
        [InlineData(0.001255, 0.001107, 0.000137, 0.9999, 0.9998, 1.0004)]
        public void Convert_Xyz_to_Luv(float x, float y, float z, float l, float u, float v)
        {
            // Arrange
            var input = new CieXyz(x, y, z);
            var options = new ColorSpaceConverterOptions { WhitePoint = Illuminants.D65, TargetLabWhitePoint = Illuminants.D65 };
            var converter = new ColorSpaceConverter(options);
            var expected = new CieLuv(l, u, v);

            Span<CieXyz> inputSpan = new CieXyz[5];
            inputSpan.Fill(input);

            Span<CieLuv> actualSpan = new CieLuv[5];

            // Act
            var actual = converter.ToCieLuv(input);
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