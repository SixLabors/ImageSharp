// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces
{
    /// <summary>
    /// Tests <see cref="Rgb"/>-<see cref="Hsl"/> conversions.
    /// </summary>
    /// <remarks>
    /// Test data generated using:
    /// <see href="http://www.colorhexa.com"/>
    /// <see href="http://www.rapidtables.com/convert/color/hsl-to-rgb"/>
    /// </remarks>
    public class RgbAndHslConversionTest
    {
        private static readonly IEqualityComparer<float> FloatRoundingComparer = new FloatRoundingComparer(4);

        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        private static readonly ApproximateFloatComparer ApproximateComparer = new ApproximateFloatComparer(0.0001F);

        /// <summary>
        /// Tests conversion from <see cref="Hsl"/> to <see cref="Rgb"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(0, 1, 1, 1, 1, 1)]
        [InlineData(360, 1, 1, 1, 1, 1)]
        [InlineData(0, 1, .5F, 1, 0, 0)]
        [InlineData(120, 1, .5F, 0, 1, 0)]
        [InlineData(240, 1, .5F, 0, 0, 1)]
        public void Convert_Hsl_To_Rgb(float h, float s, float l, float r, float g, float b)
        {
            // Arrange
            var input = new Hsl(h, s, l);

            // Act
            Rgb output = Converter.ToRgb(input);

            // Assert
            Assert.Equal(Rgb.DefaultWorkingSpace, output.WorkingSpace, ApproximateComparer);
            Assert.Equal(r, output.R, FloatRoundingComparer);
            Assert.Equal(g, output.G, FloatRoundingComparer);
            Assert.Equal(b, output.B, FloatRoundingComparer);
        }

        /// <summary>
        /// Tests conversion from <see cref="Rgb"/> to <see cref="Hsl"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 0, 0)]
        [InlineData(1, 1, 1, 0, 0, 1)]
        [InlineData(1, 0, 0, 0, 1, .5F)]
        [InlineData(0, 1, 0, 120, 1, .5F)]
        [InlineData(0, 0, 1, 240, 1, .5F)]
        public void Convert_Rgb_To_Hsl(float r, float g, float b, float h, float s, float l)
        {
            // Arrange
            var input = new Rgb(r, g, b);

            // Act
            Hsl output = Converter.ToHsl(input);

            // Assert
            Assert.Equal(h, output.H, FloatRoundingComparer);
            Assert.Equal(s, output.S, FloatRoundingComparer);
            Assert.Equal(l, output.L, FloatRoundingComparer);
        }
    }
}
