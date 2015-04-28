// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ColorConversionTests.cs" company="James South">
//   Copyright © James South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Test conversion between the various color structs.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Tests
{
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;

    using Xunit;

    /// <summary>
    /// Test conversion between the various color structs.
    /// </summary>
    public class ColorConversionTests
    {
        /// <summary>
        /// Tests the implicit conversion from <see cref="Color"/> to <see cref="YCbCrColor"/>.
        /// </summary>
        [Fact]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1305:FieldNamesMustNotUseHungarianNotation", Justification = "Reviewed. Suppression is OK here.")]
        public void ColorToYCbCrColor()
        {
            // White
            Color color = new Color(255, 255, 255, 255);
            YCbCrColor yCbCrColor = color;

            Assert.Equal(255, yCbCrColor.Y);
            Assert.Equal(128, yCbCrColor.Cb);
            Assert.Equal(128, yCbCrColor.Cr);

            // Black
            Color color2 = new Color(0, 0, 0, 255);
            YCbCrColor yCbCrColor2 = color2;
            Assert.Equal(0, yCbCrColor2.Y);
            Assert.Equal(128, yCbCrColor2.Cb);
            Assert.Equal(128, yCbCrColor2.Cr);


            // Grey
            Color color3 = new Color(128, 128, 128, 255);
            YCbCrColor yCbCrColor3 = color3;
            Assert.Equal(128, yCbCrColor3.Y);
            Assert.Equal(128, yCbCrColor3.Cb);
            Assert.Equal(128, yCbCrColor3.Cr);
        }

        /// <summary>
        /// Tests the implicit conversion from <see cref="YCbCrColor"/> to <see cref="Color"/>.
        /// </summary>
        [Fact]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1305:FieldNamesMustNotUseHungarianNotation", Justification = "Reviewed. Suppression is OK here.")]
        public void YCbCrColorToColor()
        {
            // White
            YCbCrColor yCbCrColor = new YCbCrColor(255, 128, 128);
            Color color = yCbCrColor;

            Assert.Equal(255, color.B);
            Assert.Equal(255, color.G);
            Assert.Equal(255, color.R);
            Assert.Equal(255, color.A);

            // Black
            YCbCrColor yCbCrColor2 = new YCbCrColor(0, 128, 128);
            Color color2 = yCbCrColor2;

            Assert.Equal(0, color2.B);
            Assert.Equal(0, color2.G);
            Assert.Equal(0, color2.R);
            Assert.Equal(255, color2.A);

            // Grey
            YCbCrColor yCbCrColor3 = new YCbCrColor(128, 128, 128);
            Color color3 = yCbCrColor3;

            Assert.Equal(128, color3.B);
            Assert.Equal(128, color3.G);
            Assert.Equal(128, color3.R);
            Assert.Equal(255, color3.A);
        }
    }
}
