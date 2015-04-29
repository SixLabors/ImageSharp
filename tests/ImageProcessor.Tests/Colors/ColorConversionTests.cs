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
        /// Tests the implicit conversion from <see cref="Bgra"/> to <see cref="YCbCr"/>.
        /// </summary>
        [Fact]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1305:FieldNamesMustNotUseHungarianNotation", Justification = "Reviewed. Suppression is OK here.")]
        public void ColorToYCbCrColor()
        {
            // White
            Bgra color = new Bgra(255, 255, 255, 255);
            YCbCr yCbCrColor = color;

            Assert.Equal(255, yCbCrColor.Y);
            Assert.Equal(128, yCbCrColor.Cb);
            Assert.Equal(128, yCbCrColor.Cr);

            // Black
            Bgra color2 = new Bgra(0, 0, 0, 255);
            YCbCr yCbCrColor2 = color2;
            Assert.Equal(0, yCbCrColor2.Y);
            Assert.Equal(128, yCbCrColor2.Cb);
            Assert.Equal(128, yCbCrColor2.Cr);

            // Grey
            Bgra color3 = new Bgra(128, 128, 128, 255);
            YCbCr yCbCrColor3 = color3;
            Assert.Equal(128, yCbCrColor3.Y);
            Assert.Equal(128, yCbCrColor3.Cb);
            Assert.Equal(128, yCbCrColor3.Cr);
        }

        /// <summary>
        /// Tests the implicit conversion from <see cref="YCbCr"/> to <see cref="Bgra"/>.
        /// </summary>
        [Fact]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1305:FieldNamesMustNotUseHungarianNotation", Justification = "Reviewed. Suppression is OK here.")]
        public void YCbCrColorToColor()
        {
            // White
            YCbCr yCbCrColor = new YCbCr(255, 128, 128);
            Bgra color = yCbCrColor;

            Assert.Equal(255, color.B);
            Assert.Equal(255, color.G);
            Assert.Equal(255, color.R);
            Assert.Equal(255, color.A);

            // Black
            YCbCr yCbCrColor2 = new YCbCr(0, 128, 128);
            Bgra color2 = yCbCrColor2;

            Assert.Equal(0, color2.B);
            Assert.Equal(0, color2.G);
            Assert.Equal(0, color2.R);
            Assert.Equal(255, color2.A);

            // Grey
            YCbCr yCbCrColor3 = new YCbCr(128, 128, 128);
            Bgra color3 = yCbCrColor3;

            Assert.Equal(128, color3.B);
            Assert.Equal(128, color3.G);
            Assert.Equal(128, color3.R);
            Assert.Equal(255, color3.A);
        }
    }
}
