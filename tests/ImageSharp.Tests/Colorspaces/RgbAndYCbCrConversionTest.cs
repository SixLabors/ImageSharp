// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colorspaces
{
    /// <summary>
    /// Tests <see cref="Rgb"/>-<see cref="YCbCr"/> conversions.
    /// </summary>
    /// <remarks>
    /// Test data generated mathematically
    /// </remarks>
    public class RgbAndYCbCrConversionTest
    {
        private static readonly IEqualityComparer<float> FloatRoundingComparer = new FloatRoundingComparer(3);

        private static readonly ColorSpaceConverter Converter = new ColorSpaceConverter();

        private static readonly ApproximateFloatComparer ApproximateComparer = new ApproximateFloatComparer(0.0001F);

        /// <summary>
        /// Tests conversion from <see cref="YCbCr"/> to <see cref="Rgb"/>.
        /// </summary>
        [Theory]
        [InlineData(255, 128, 128, 1, 1, 1)]
        [InlineData(0, 128, 128, 0, 0, 0)]
        [InlineData(128, 128, 128, 0.502, 0.502, 0.502)]
        public void Convert_YCbCr_To_Rgb(float y, float cb, float cr, float r, float g, float b)
        {
            // Arrange
            var input = new YCbCr(y, cb, cr);

            // Act
            Rgb output = Converter.ToRgb(input);

            // Assert
            Assert.Equal(Rgb.DefaultWorkingSpace, output.WorkingSpace, ApproximateComparer);
            Assert.Equal(r, output.R, FloatRoundingComparer);
            Assert.Equal(g, output.G, FloatRoundingComparer);
            Assert.Equal(b, output.B, FloatRoundingComparer);
        }

        /// <summary>
        /// Tests conversion from <see cref="Rgb"/> to <see cref="YCbCr"/>.
        /// </summary>
        [Theory]
        [InlineData(0, 0, 0, 0, 128, 128)]
        [InlineData(1, 1, 1, 255, 128, 128)]
        [InlineData(0.5, 0.5, 0.5, 127.5, 128, 128)]
        [InlineData(1, 0, 0, 76.245, 84.972, 255)]
        public void Convert_Rgb_To_YCbCr(float r, float g, float b, float y, float cb, float cr)
        {
            // Arrange
            var input = new Rgb(r, g, b);

            // Act
            YCbCr output = Converter.ToYCbCr(input);

            // Assert
            Assert.Equal(y, output.Y, FloatRoundingComparer);
            Assert.Equal(cb, output.Cb, FloatRoundingComparer);
            Assert.Equal(cr, output.Cr, FloatRoundingComparer);
        }
    }
}