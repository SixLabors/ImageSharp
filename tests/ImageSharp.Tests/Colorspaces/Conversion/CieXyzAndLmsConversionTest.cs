// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Conversion
{
    /// <summary>
    /// Tests <see cref="CieXyz"/>-<see cref="Lms"/> conversions.
    /// </summary>
    /// <remarks>
    /// Test data generated using original colorful library.
    /// </remarks>
    public class CieXyzAndLmsConversionTest
    {
        private static readonly ApproximateColorSpaceComparer ColorSpaceComparer = new ApproximateColorSpaceComparer(.0001F);

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
            var expected = new CieXyz(x, y, z);

            Span<Lms> inputSpan = new Lms[5];
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
            var expected = new Lms(l, m, s);

            Span<CieXyz> inputSpan = new CieXyz[5];
            inputSpan.Fill(input);

            Span<Lms> actualSpan = new Lms[5];

            // Act
            var actual = converter.ToLms(input);
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