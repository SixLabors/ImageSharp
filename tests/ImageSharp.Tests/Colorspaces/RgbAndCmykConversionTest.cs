// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces
{
    /// <summary>
    /// Tests <see cref="Rgb"/>-<see cref="Cmyk"/> conversions.
    /// </summary>
    /// <remarks>
    /// Test data generated using:
    /// <see href="http://www.colorhexa.com"/>
    /// <see href="http://www.rapidtables.com/convert/color/cmyk-to-rgb.htm"/>
    /// </remarks>
    public class RgbAndCmykConversionTest
    {
        private static readonly IEqualityComparer<float> FloatRoundingComparer = new FloatRoundingComparer(4);

        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        private static readonly ApproximateFloatComparer ApproximateComparer = new ApproximateFloatComparer(0.0001F);

        /// <summary>
        /// Tests conversion from <see cref="Cmyk"/> to <see cref="Rgb"/>.
        /// </summary>
        [Theory]
        [InlineData(1, 1, 1, 1, 0, 0, 0)]
        [InlineData(0, 0, 0, 0, 1, 1, 1)]
        [InlineData(0, 0.84, 0.037, 0.365, 0.635, 0.1016, 0.6115)]
        public void Convert_Cmyk_To_Rgb(float c, float m, float y, float k, float r, float g, float b)
        {
            // Arrange
            var input = new Cmyk(c, m, y, k);

            // Act
            Rgb output = Converter.ToRgb(input);

            // Assert
            Assert.Equal(Rgb.DefaultWorkingSpace, output.WorkingSpace, ApproximateComparer);
            Assert.Equal(r, output.R, FloatRoundingComparer);
            Assert.Equal(g, output.G, FloatRoundingComparer);
            Assert.Equal(b, output.B, FloatRoundingComparer);
        }

        /// <summary>
        /// Tests conversion from <see cref="Rgb"/> to <see cref="Cmyk"/>.
        /// </summary>
        [Theory]
        [InlineData(1, 1, 1, 0, 0, 0, 0)]
        [InlineData(0, 0, 0, 0, 0, 0, 1)]
        [InlineData(0.635, 0.1016, 0.6115, 0, 0.84, 0.037, 0.365)]
        public void Convert_Rgb_To_Cmyk(float r, float g, float b, float c, float m, float y, float k)
        {
            // Arrange
            var input = new Rgb(r, g, b);

            // Act
            Cmyk output = Converter.ToCmyk(input);

            // Assert
            Assert.Equal(c, output.C, FloatRoundingComparer);
            Assert.Equal(m, output.M, FloatRoundingComparer);
            Assert.Equal(y, output.Y, FloatRoundingComparer);
            Assert.Equal(k, output.K, FloatRoundingComparer);
        }
    }
}
