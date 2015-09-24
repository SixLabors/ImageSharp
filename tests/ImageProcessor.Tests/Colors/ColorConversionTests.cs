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
        /// Tests the implicit conversion from <see cref="Bgra"/> to <see cref="YCbCr"/>.
        /// </summary>
        [Fact]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1305:FieldNamesMustNotUseHungarianNotation",
            Justification = "Reviewed. Suppression is OK here.")]
        public void BgrToYCbCr()
        {
            // White
            Bgra color = new Bgra(255, 255, 255, 255);
            YCbCr yCbCr = color;

            Assert.Equal(255, yCbCr.Y);
            Assert.Equal(128, yCbCr.Cb);
            Assert.Equal(128, yCbCr.Cr);

            // Black
            Bgra color2 = new Bgra(0, 0, 0, 255);
            YCbCr yCbCr2 = color2;
            Assert.Equal(0, yCbCr2.Y);
            Assert.Equal(128, yCbCr2.Cb);
            Assert.Equal(128, yCbCr2.Cr);

            // Grey
            Bgra color3 = new Bgra(128, 128, 128, 255);
            YCbCr yCbCr3 = color3;
            Assert.Equal(128, yCbCr3.Y);
            Assert.Equal(128, yCbCr3.Cb);
            Assert.Equal(128, yCbCr3.Cr);
        }

        /// <summary>
        /// Tests the implicit conversion from <see cref="YCbCr"/> to <see cref="Bgra"/>.
        /// </summary>
        [Fact]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1305:FieldNamesMustNotUseHungarianNotation",
            Justification = "Reviewed. Suppression is OK here.")]
        public void YCbCrToBgr()
        {
            // White
            YCbCr yCbCr = new YCbCr(255, 128, 128);
            Bgra color = yCbCr;

            Assert.Equal(255, color.B);
            Assert.Equal(255, color.G);
            Assert.Equal(255, color.R);
            Assert.Equal(255, color.A);

            // Black
            YCbCr yCbCr2 = new YCbCr(0, 128, 128);
            Bgra color2 = yCbCr2;

            Assert.Equal(0, color2.B);
            Assert.Equal(0, color2.G);
            Assert.Equal(0, color2.R);
            Assert.Equal(255, color2.A);

            // Grey
            YCbCr yCbCr3 = new YCbCr(128, 128, 128);
            Bgra color3 = yCbCr3;

            Assert.Equal(128, color3.B);
            Assert.Equal(128, color3.G);
            Assert.Equal(128, color3.R);
            Assert.Equal(255, color3.A);
        }

        /// <summary>
        /// Tests the implicit conversion from <see cref="Bgra"/> to <see cref="Hsv"/>.
        /// </summary>
        [Fact]
        [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1305:FieldNamesMustNotUseHungarianNotation",
            Justification = "Reviewed. Suppression is OK here.")]
        public void BgrToHsv()
        {
            // Black
            Bgra b = new Bgra(0, 0, 0, 255);
            Hsv h = b;

            Assert.Equal(0, h.H);
            Assert.Equal(0, h.S);
            Assert.Equal(0, h.V);

            // White
            Bgra color = new Bgra(255, 255, 255, 255);
            Hsv hsv = color;

            Assert.Equal(0, hsv.H);
            Assert.Equal(0, hsv.S);
            Assert.Equal(100, hsv.V);

            // Dark moderate pink.
            Bgra color2 = new Bgra(106, 64, 128, 255);
            Hsv hsv2 = color2;

            Assert.Equal(320.6, hsv2.H, 1);
            Assert.Equal(50, hsv2.S, 1);
            Assert.Equal(50.2, hsv2.V, 1);

            // Ochre.
            Bgra color3 = new Bgra(34, 119, 204, 255);
            Hsv hsv3 = color3;

            Assert.Equal(30, hsv3.H, 1);
            Assert.Equal(83.3, hsv3.S, 1);
            Assert.Equal(80, hsv3.V, 1);
        }

        /// <summary>
        /// Tests the implicit conversion from <see cref="Hsv"/> to <see cref="Bgra"/>.
        /// </summary>
        [Fact]
        public void HsvToBgr()
        {
            // Dark moderate pink.
            Hsv hsv = new Hsv(320.6f, 50, 50.2f);
            Bgra bgra = hsv;

            Assert.Equal(bgra.B, 106);
            Assert.Equal(bgra.G, 64);
            Assert.Equal(bgra.R, 128);

            // Ochre
            Hsv hsv2 = new Hsv(30, 83.3f, 80);
            Bgra bgra2 = hsv2;

            Assert.Equal(bgra2.B, 34);
            Assert.Equal(bgra2.G, 119);
            Assert.Equal(bgra2.R, 204);

            // White
            Hsv hsv3 = new Hsv(0, 0, 100);
            Bgra bgra3 = hsv3;

            Assert.Equal(bgra3.B, 255);
            Assert.Equal(bgra3.G, 255);
            Assert.Equal(bgra3.R, 255);

            // Check others.
            Random random = new Random(0);
            for (var i = 0; i < 1000; i++)
            {
                Bgra bgra4 = new Bgra((byte)random.Next(255), (byte)random.Next(255), (byte)random.Next(255));
                Hsv hsb4 = bgra4;
                Assert.Equal(bgra4, (Bgra)hsb4);
            }
        }
    }
}
