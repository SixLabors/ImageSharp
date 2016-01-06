// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ColorConversionTests.cs" company="James Jackson-South">
//   Copyright © James Jackson-South and contributors.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   Test conversion between the various color structs.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Tests
{
    using System;
    using System.Diagnostics.CodeAnalysis;

    using Xunit;

    /// <summary>
    /// Test conversion between the various color structs.
    ///  </summary>
    /// <remarks>
    /// Output values have been compared with <see cref="http://colormine.org/color-converter"/>
    /// and <see cref="http://www.colorhexa.com/"/> for accuracy.
    /// </remarks>
    public class ColorConversionTests
    {
        /// <summary>
        /// Tests the implicit conversion from <see cref="Color"/> to <see cref="YCbCr"/>.
        /// </summary>
        [Fact]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1305:FieldNamesMustNotUseHungarianNotation",
            Justification = "Reviewed. Suppression is OK here.")]
        public void ColorToYCbCr()
        {
            // White
            Color color = new Color(1, 1, 1);
            YCbCr yCbCr = color;

            Assert.Equal(255, yCbCr.Y, 0);
            Assert.Equal(128, yCbCr.Cb, 0);
            Assert.Equal(128, yCbCr.Cr, 0);

            // Black
            Color color2 = new Color(0, 0, 0);
            YCbCr yCbCr2 = color2;
            Assert.Equal(0, yCbCr2.Y, 0);
            Assert.Equal(128, yCbCr2.Cb, 0);
            Assert.Equal(128, yCbCr2.Cr, 0);

            // Grey
            Color color3 = new Color(.5f, .5f, .5f);
            YCbCr yCbCr3 = color3;
            Assert.Equal(128, yCbCr3.Y, 0);
            Assert.Equal(128, yCbCr3.Cb, 0);
            Assert.Equal(128, yCbCr3.Cr, 0);
        }

        /// <summary>
        /// Tests the implicit conversion from <see cref="YCbCr"/> to <see cref="Color"/>.
        /// </summary>
        [Fact]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1305:FieldNamesMustNotUseHungarianNotation",
            Justification = "Reviewed. Suppression is OK here.")]
        public void YCbCrToColor()
        {
            // White
            YCbCr yCbCr = new YCbCr(255, 128, 128);
            Color color = yCbCr;

            Assert.Equal(1f, color.R, 1);
            Assert.Equal(1f, color.G, 1);
            Assert.Equal(1f, color.B, 1);
            Assert.Equal(1f, color.A, 1);

            // Black
            YCbCr yCbCr2 = new YCbCr(0, 128, 128);
            Color color2 = yCbCr2;

            Assert.Equal(0, color2.R);
            Assert.Equal(0, color2.G);
            Assert.Equal(0, color2.B);
            Assert.Equal(1, color2.A);

            // Grey
            YCbCr yCbCr3 = new YCbCr(128, 128, 128);
            Color color3 = yCbCr3;

            Assert.Equal(.5f, color3.R, 1);
            Assert.Equal(.5f, color3.G, 1);
            Assert.Equal(.5f, color3.B, 1);
            Assert.Equal(1f, color3.A, 1);
        }

        /// <summary>
        /// Tests the implicit conversion from <see cref="Color"/> to <see cref="CieXyz"/>.
        /// Comparison values obtained from 
        /// http://colormine.org/convert/rgb-to-xyz
        /// </summary>
        [Fact]
        public void ColorToCieXyz()
        {
            // White
            Color color = new Color(1, 1, 1);
            CieXyz ciexyz = color;

            Assert.Equal(95.05f, ciexyz.X, 3);
            Assert.Equal(100.0f, ciexyz.Y, 3);
            Assert.Equal(108.900f, ciexyz.Z, 3);

            // Black
            Color color2 = new Color(0, 0, 0);
            CieXyz ciexyz2 = color2;
            Assert.Equal(0, ciexyz2.X, 3);
            Assert.Equal(0, ciexyz2.Y, 3);
            Assert.Equal(0, ciexyz2.Z, 3);

            //// Grey
            Color color3 = new Color(128 / 255f, 128 / 255f, 128 / 255f);
            CieXyz ciexyz3 = color3;
            Assert.Equal(20.518, ciexyz3.X, 3);
            Assert.Equal(21.586, ciexyz3.Y, 3);
            Assert.Equal(23.507, ciexyz3.Z, 3);

            //// Cyan
            Color color4 = new Color(0, 1, 1);
            CieXyz ciexyz4 = color4;
            Assert.Equal(53.810f, ciexyz4.X, 3);
            Assert.Equal(78.740f, ciexyz4.Y, 3);
            Assert.Equal(106.970f, ciexyz4.Z, 3);
        }

        /// <summary>
        /// Tests the implicit conversion from <see cref="CieXyz"/> to <see cref="Color"/>.
        /// Comparison values obtained from 
        /// http://colormine.org/convert/rgb-to-xyz
        /// </summary>
        [Fact]
        public void CieXyzToColor()
        {
            // Dark moderate pink.
            CieXyz ciexyz = new CieXyz(13.337f, 9.297f, 14.727f);
            Color color = ciexyz;

            Assert.Equal(128 / 255f, color.R, 3);
            Assert.Equal(64 / 255f, color.G, 3);
            Assert.Equal(106 / 255f, color.B, 3);

            // Ochre
            CieXyz ciexyz2 = new CieXyz(31.787f, 26.147f, 4.885f);
            Color color2 = ciexyz2;

            Assert.Equal(204 / 255f, color2.R, 3);
            Assert.Equal(119 / 255f, color2.G, 3);
            Assert.Equal(34 / 255f, color2.B, 3);

            //// White
            CieXyz ciexyz3 = new CieXyz(0, 0, 0);
            Color color3 = ciexyz3;

            Assert.Equal(0f, color3.R, 3);
            Assert.Equal(0f, color3.G, 3);
            Assert.Equal(0f, color3.B, 3);

            //// Check others.
            //Random random = new Random(0);
            //for (int i = 0; i < 1000; i++)
            //{
            //    Color color4 = new Color((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());
            //    CieXyz ciexyz4 = color4;
            //    Assert.Equal(color4, (Color)ciexyz4);
            //}
        }

        /// <summary>
        /// Tests the implicit conversion from <see cref="Color"/> to <see cref="Hsv"/>.
        /// </summary>
        [Fact]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1305:FieldNamesMustNotUseHungarianNotation",
            Justification = "Reviewed. Suppression is OK here.")]
        public void ColorToHsv()
        {
            // Black
            Color b = new Color(0, 0, 0);
            Hsv h = b;

            Assert.Equal(0, h.H, 1);
            Assert.Equal(0, h.S, 1);
            Assert.Equal(0, h.V, 1);

            // White
            Color color = new Color(1, 1, 1);
            Hsv hsv = color;

            Assert.Equal(0f, hsv.H, 1);
            Assert.Equal(0f, hsv.S, 1);
            Assert.Equal(1f, hsv.V, 1);

            // Dark moderate pink.
            Color color2 = new Color(128 / 255f, 64 / 255f, 106 / 255f);
            Hsv hsv2 = color2;

            Assert.Equal(320.6f, hsv2.H, 1);
            Assert.Equal(0.5f, hsv2.S, 1);
            Assert.Equal(0.502f, hsv2.V, 2);

            // Ochre.
            Color color3 = new Color(204 / 255f, 119 / 255f, 34 / 255f);
            Hsv hsv3 = color3;

            Assert.Equal(30f, hsv3.H, 1);
            Assert.Equal(0.833f, hsv3.S, 3);
            Assert.Equal(0.8f, hsv3.V, 1);
        }

        /// <summary>
        /// Tests the implicit conversion from <see cref="Hsv"/> to <see cref="Color"/>.
        /// </summary>
        [Fact]
        public void HsvToColor()
        {
            // Dark moderate pink.
            Hsv hsv = new Hsv(320.6f, 0.5f, 0.502f);
            Color color = hsv;

            Assert.Equal(color.B, 106 / 255f, 1);
            Assert.Equal(color.G, 64 / 255f, 1);
            Assert.Equal(color.R, 128 / 255f, 1);

            // Ochre
            Hsv hsv2 = new Hsv(30, 0.833f, 0.8f);
            Color color2 = hsv2;

            Assert.Equal(color2.B, 34 / 255f, 1);
            Assert.Equal(color2.G, 119 / 255f, 1);
            Assert.Equal(color2.R, 204 / 255f, 1);

            // White
            Hsv hsv3 = new Hsv(0, 0, 1);
            Color color3 = hsv3;

            Assert.Equal(color3.B, 1, 1);
            Assert.Equal(color3.G, 1, 1);
            Assert.Equal(color3.R, 1, 1);

            // Check others.
            //Random random = new Random(0);
            //for (int i = 0; i < 1000; i++)
            //{
            //    Color color4 = new Color((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());
            //    Hsv hsv4 = color4;
            //    Assert.Equal(color4, (Color)hsv4);
            //}
        }

        /// <summary>
        /// Tests the implicit conversion from <see cref="Color"/> to <see cref="Hsl"/>.
        /// </summary>
        [Fact]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1305:FieldNamesMustNotUseHungarianNotation",
            Justification = "Reviewed. Suppression is OK here.")]
        public void ColorToHsl()
        {
            // Black
            Color b = new Color(0, 0, 0);
            Hsl h = b;

            Assert.Equal(0, h.H, 1);
            Assert.Equal(0, h.S, 1);
            Assert.Equal(0, h.L, 1);

            // White
            Color color = new Color(1, 1, 1);
            Hsl hsl = color;

            Assert.Equal(0f, hsl.H, 1);
            Assert.Equal(0f, hsl.S, 1);
            Assert.Equal(1f, hsl.L, 1);

            // Dark moderate pink.
            Color color2 = new Color(128 / 255f, 64 / 255f, 106 / 255f);
            Hsl hsl2 = color2;

            Assert.Equal(320.6f, hsl2.H, 1);
            Assert.Equal(0.33f, hsl2.S, 1);
            Assert.Equal(0.376f, hsl2.L, 2);

            // Ochre.
            Color color3 = new Color(204 / 255f, 119 / 255f, 34 / 255f);
            Hsl hsl3 = color3;

            Assert.Equal(30f, hsl3.H, 1);
            Assert.Equal(0.714f, hsl3.S, 3);
            Assert.Equal(0.467f, hsl3.L, 3);
        }

        /// <summary>
        /// Tests the implicit conversion from <see cref="Hsl"/> to <see cref="Color"/>.
        /// </summary>
        [Fact]
        public void HslToColor()
        {
            // Dark moderate pink.
            Hsl hsl = new Hsl(320.6f, 0.33f, 0.376f);
            Color color = hsl;

            Assert.Equal(color.B, 106 / 255f, 1);
            Assert.Equal(color.G, 64 / 255f, 1);
            Assert.Equal(color.R, 128 / 255f, 1);

            // Ochre
            Hsl hsl2 = new Hsl(30, 0.714f, 0.467f);
            Color color2 = hsl2;

            Assert.Equal(color2.B, 34 / 255f, 1);
            Assert.Equal(color2.G, 119 / 255f, 1);
            Assert.Equal(color2.R, 204 / 255f, 1);

            // White
            Hsl hsl3 = new Hsl(0, 0, 1);
            Color color3 = hsl3;

            Assert.Equal(color3.B, 1, 1);
            Assert.Equal(color3.G, 1, 1);
            Assert.Equal(color3.R, 1, 1);

            // Check others.
            //Random random = new Random(0);
            //for (int i = 0; i < 1000; i++)
            //{
            //    Color color4 = new Color((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());
            //    Hsl hsl4 = color4;
            //    Assert.Equal(color4, (Color)hsl4);
            //}
        }

        /// <summary>
        /// Tests the implicit conversion from <see cref="Color"/> to <see cref="Cmyk"/>.
        /// </summary>
        [Fact]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1305:FieldNamesMustNotUseHungarianNotation",
            Justification = "Reviewed. Suppression is OK here.")]
        public void ColorToCmyk()
        {
            // White
            Color color = new Color(1, 1, 1);
            Cmyk cmyk = color;

            Assert.Equal(0, cmyk.C, 1);
            Assert.Equal(0, cmyk.M, 1);
            Assert.Equal(0, cmyk.Y, 1);
            Assert.Equal(0, cmyk.K, 1);

            // Black
            Color color2 = new Color(0, 0, 0);
            Cmyk cmyk2 = color2;
            Assert.Equal(0, cmyk2.C, 1);
            Assert.Equal(0, cmyk2.M, 1);
            Assert.Equal(0, cmyk2.Y, 1);
            Assert.Equal(1, cmyk2.K, 1);

            // Grey
            Color color3 = new Color(128 / 255f, 128 / 255f, 128 / 255f);
            Cmyk cmyk3 = color3;
            Assert.Equal(0f, cmyk3.C, 1);
            Assert.Equal(0f, cmyk3.M, 1);
            Assert.Equal(0f, cmyk3.Y, 1);
            Assert.Equal(0.498, cmyk3.K, 2); // Checked with other online converters.

            // Cyan
            Color color4 = new Color(0, 1, 1);
            Cmyk cmyk4 = color4;
            Assert.Equal(1, cmyk4.C, 1);
            Assert.Equal(0f, cmyk4.M, 1);
            Assert.Equal(0f, cmyk4.Y, 1);
            Assert.Equal(0f, cmyk4.K, 1);
        }

        /// <summary>
        /// Tests the implicit conversion from <see cref="Cmyk"/> to <see cref="Color"/>.
        /// </summary>
        [Fact]
        public void CmykToColor()
        {
            // Dark moderate pink.
            Cmyk cmyk = new Cmyk(0f, .5f, .171f, .498f);
            Color color = cmyk;

            Assert.Equal(color.R, 128 / 255f, 1);
            Assert.Equal(color.G, 64 / 255f, 1);
            Assert.Equal(color.B, 106 / 255f, 1);

            // Ochre
            Cmyk cmyk2 = new Cmyk(0, .416f, .833f, .199f);
            Color color2 = cmyk2;

            Assert.Equal(color2.R, 204 / 255f, 1);
            Assert.Equal(color2.G, 119 / 255f, 1);
            Assert.Equal(color2.B, 34 / 255f, 1);

            // White
            Cmyk cmyk3 = new Cmyk(0, 0, 0, 0);
            Color color3 = cmyk3;

            Assert.Equal(color3.R, 1f, 1);
            Assert.Equal(color3.G, 1f, 1);
            Assert.Equal(color3.B, 1f, 1);

            // Check others.
            //Random random = new Random(0);
            //for (int i = 0; i < 1000; i++)
            //{
            //    Color color4 = new Color((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());
            //    Cmyk cmyk4 = color4;
            //    Assert.Equal(color4, (Color)cmyk4);
            //}
        }

        /// <summary>
        /// Tests the implicit conversion from <see cref="Color"/> to <see cref="CieLab"/>.
        /// Comparison values obtained from 
        /// http://colormine.org/convert/rgb-to-lab
        /// </summary>
        [Fact]
        public void ColorToCieLab()
        {
            // White
            Color color = new Color(1, 1, 1);
            CieLab cielab = color;

            Assert.Equal(100, cielab.L, 3);
            Assert.Equal(0.005, cielab.A, 3);
            Assert.Equal(-0.010, cielab.B, 3);

            // Black
            Color color2 = new Color(0, 0, 0);
            CieLab cielab2 = color2;
            Assert.Equal(0, cielab2.L, 3);
            Assert.Equal(0, cielab2.A, 3);
            Assert.Equal(0, cielab2.B, 3);

            //// Grey
            Color color3 = new Color(128 / 255f, 128 / 255f, 128 / 255f);
            CieLab cielab3 = color3;
            Assert.Equal(53.585, cielab3.L, 3);
            Assert.Equal(0.003, cielab3.A, 3);
            Assert.Equal(-0.006, cielab3.B, 3);

            //// Cyan
            Color color4 = new Color(0, 1, 1);
            CieLab cielab4 = color4;
            Assert.Equal(91.117, cielab4.L, 3);
            Assert.Equal(-48.080, cielab4.A, 3);
            Assert.Equal(-14.138, cielab4.B, 3);
        }

        /// <summary>
        /// Tests the implicit conversion from <see cref="CieLab"/> to <see cref="Color"/>.
        /// </summary>
        /// Comparison values obtained from 
        /// http://colormine.org/convert/rgb-to-lab
        [Fact]
        public void CieLabToColor()
        {
            // Dark moderate pink.
            CieLab cielab = new CieLab(36.5492f, 33.3173f, -12.0615f);
            Color color = cielab;

            Assert.Equal(color.R, 128 / 255f, 3);
            Assert.Equal(color.G, 64 / 255f, 3);
            Assert.Equal(color.B, 106 / 255f, 3);

            // Ochre
            CieLab cielab2 = new CieLab(58.1758f, 27.3399f, 56.8240f);
            Color color2 = cielab2;

            Assert.Equal(color2.R, 204 / 255f, 3);
            Assert.Equal(color2.G, 119 / 255f, 3);
            Assert.Equal(color2.B, 34 / 255f, 3);

            // White
            CieLab cielab3 = new CieLab(0, 0, 0);
            Color color3 = cielab3;

            Assert.Equal(color3.R, 0f, 3);
            Assert.Equal(color3.G, 0f, 3);
            Assert.Equal(color3.B, 0f, 3);

            // Check others.
            //Random random = new Random(0);
            //for (int i = 0; i < 1000; i++)
            //{
            //    Color color4 = new Color((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble());
            //    CieLab cielab4 = color4;
            //    Assert.Equal(color4, (Color)cielab4);
            //}
        }
    }
}
