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
    using System;
    using System.Diagnostics.CodeAnalysis;

    using Xunit;

    /// <summary>
    /// Test conversion between the various color structs.
    /// </summary>
    public class ColorConversionTests
    {
        /// <summary>
        /// Tests the implicit conversion from <see cref="Bgra32"/> to <see cref="YCbCr"/>.
        /// </summary>
        [Fact]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1305:FieldNamesMustNotUseHungarianNotation",
            Justification = "Reviewed. Suppression is OK here.")]
        public void BgrToYCbCr()
        {
            // White
            Bgra32 color = new Bgra32(255, 255, 255, 255);
            YCbCr yCbCr = color;

            Assert.Equal(255, yCbCr.Y);
            Assert.Equal(128, yCbCr.Cb);
            Assert.Equal(128, yCbCr.Cr);

            // Black
            Bgra32 color2 = new Bgra32(0, 0, 0, 255);
            YCbCr yCbCr2 = color2;
            Assert.Equal(0, yCbCr2.Y);
            Assert.Equal(128, yCbCr2.Cb);
            Assert.Equal(128, yCbCr2.Cr);

            // Grey
            Bgra32 color3 = new Bgra32(128, 128, 128, 255);
            YCbCr yCbCr3 = color3;
            Assert.Equal(128, yCbCr3.Y);
            Assert.Equal(128, yCbCr3.Cb);
            Assert.Equal(128, yCbCr3.Cr);
        }

        /// <summary>
        /// Tests the implicit conversion from <see cref="YCbCr"/> to <see cref="Bgra32"/>.
        /// </summary>
        [Fact]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1305:FieldNamesMustNotUseHungarianNotation",
            Justification = "Reviewed. Suppression is OK here.")]
        public void YCbCrToBgr()
        {
            // White
            YCbCr yCbCr = new YCbCr(255, 128, 128);
            Bgra32 color = yCbCr;

            Assert.Equal(255, color.B);
            Assert.Equal(255, color.G);
            Assert.Equal(255, color.R);
            Assert.Equal(255, color.A);

            // Black
            YCbCr yCbCr2 = new YCbCr(0, 128, 128);
            Bgra32 color2 = yCbCr2;

            Assert.Equal(0, color2.B);
            Assert.Equal(0, color2.G);
            Assert.Equal(0, color2.R);
            Assert.Equal(255, color2.A);

            // Grey
            YCbCr yCbCr3 = new YCbCr(128, 128, 128);
            Bgra32 color3 = yCbCr3;

            Assert.Equal(128, color3.B);
            Assert.Equal(128, color3.G);
            Assert.Equal(128, color3.R);
            Assert.Equal(255, color3.A);
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
            Random random = new Random(0);
            for (int i = 0; i < 1000; i++)
            {
                Color color4 = new Color(random.Next(1), random.Next(1), random.Next(1));
                Hsv hsb4 = color4;
                Assert.Equal(color4, (Color)hsb4);
            }
        }

        /// <summary>
        /// Tests the implicit conversion from <see cref="Bgra32"/> to <see cref="Cmyk"/>.
        /// </summary>
        [Fact]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1305:FieldNamesMustNotUseHungarianNotation",
            Justification = "Reviewed. Suppression is OK here.")]
        public void BgrToCmyk()
        {
            // White
            Bgra32 color = new Bgra32(255, 255, 255, 255);
            Cmyk cmyk = color;

            Assert.Equal(0, cmyk.C);
            Assert.Equal(0, cmyk.M);
            Assert.Equal(0, cmyk.Y);
            Assert.Equal(0, cmyk.K);

            // Black
            Bgra32 color2 = new Bgra32(0, 0, 0, 255);
            Cmyk cmyk2 = color2;
            Assert.Equal(0, cmyk2.C);
            Assert.Equal(0, cmyk2.M);
            Assert.Equal(0, cmyk2.Y);
            Assert.Equal(100, cmyk2.K);

            // Grey
            Bgra32 color3 = new Bgra32(128, 128, 128, 255);
            Cmyk cmyk3 = color3;
            Assert.Equal(0, cmyk3.C);
            Assert.Equal(0, cmyk3.M);
            Assert.Equal(0, cmyk3.Y);
            Assert.Equal(49.8, cmyk3.K, 1); // Checked with other tools.

            // Cyan
            Bgra32 color4 = new Bgra32(255, 255, 0, 255);
            Cmyk cmyk4 = color4;
            Assert.Equal(100, cmyk4.C);
            Assert.Equal(0, cmyk4.M);
            Assert.Equal(0, cmyk4.Y);
            Assert.Equal(0, cmyk4.K);
        }

        /// <summary>
        /// Tests the implicit conversion from <see cref="Hsv"/> to <see cref="Bgra32"/>.
        /// </summary>
        [Fact]
        public void CmykToBgr()
        {
            // Dark moderate pink.
            Cmyk cmyk = new Cmyk(49.8f, 74.9f, 58.4f, 0);
            Bgra32 bgra32 = cmyk;

            Assert.Equal(bgra32.B, 106);
            Assert.Equal(bgra32.G, 64);
            Assert.Equal(bgra32.R, 128);

            // Ochre
            Cmyk cmyk2 = new Cmyk(20, 53.3f, 86.7f, 0);
            Bgra32 bgra2 = cmyk2;

            Assert.Equal(bgra2.B, 34);
            Assert.Equal(bgra2.G, 119);
            Assert.Equal(bgra2.R, 204);

            // White
            Cmyk cmyk3 = new Cmyk(0, 0, 0, 0);
            Bgra32 bgra3 = cmyk3;

            Assert.Equal(bgra3.B, 255);
            Assert.Equal(bgra3.G, 255);
            Assert.Equal(bgra3.R, 255);

            // Check others.
            Random random = new Random(0);
            for (int i = 0; i < 1000; i++)
            {
                Bgra32 bgra4 = new Bgra32((byte)random.Next(255), (byte)random.Next(255), (byte)random.Next(255));
                Cmyk cmyk4 = bgra4;
                Assert.Equal(bgra4, (Bgra32)cmyk4);
            }
        }
    }
}
