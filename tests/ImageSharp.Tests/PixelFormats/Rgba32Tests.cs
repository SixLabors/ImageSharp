// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// Tests the <see cref="Rgba32"/> struct.
    /// </summary>
    public class Rgba32Tests
    {
        /// <summary>
        /// Tests the equality operators for equality.
        /// </summary>
        [Fact]
        public void AreEqual()
        {
            Rgba32 color1 = new Rgba32(0, 0, 0);
            Rgba32 color2 = new Rgba32(0, 0, 0, 1F);
            Rgba32 color3 = Rgba32.FromHex("#000");
            Rgba32 color4 = Rgba32.FromHex("#000F");
            Rgba32 color5 = Rgba32.FromHex("#000000");
            Rgba32 color6 = Rgba32.FromHex("#000000FF");

            Assert.Equal(color1, color2);
            Assert.Equal(color1, color3);
            Assert.Equal(color1, color4);
            Assert.Equal(color1, color5);
            Assert.Equal(color1, color6);
        }

        /// <summary>
        /// Tests the equality operators for inequality.
        /// </summary>
        [Fact]
        public void AreNotEqual()
        {
            Rgba32 color1 = new Rgba32(255, 0, 0, 255);
            Rgba32 color2 = new Rgba32(0, 0, 0, 255);
            Rgba32 color3 = Rgba32.FromHex("#000");
            Rgba32 color4 = Rgba32.FromHex("#000000");
            Rgba32 color5 = Rgba32.FromHex("#FF000000");

            Assert.NotEqual(color1, color2);
            Assert.NotEqual(color1, color3);
            Assert.NotEqual(color1, color4);
            Assert.NotEqual(color1, color5);
        }

        /// <summary>
        /// Tests whether the color constructor correctly assign properties.
        /// </summary>
        [Fact]
        public void ConstructorAssignsProperties()
        {
            Rgba32 color1 = new Rgba32(1, .1f, .133f, .864f);
            Assert.Equal(255, color1.R);
            Assert.Equal((byte)Math.Round(.1f * 255), color1.G);
            Assert.Equal((byte)Math.Round(.133f * 255), color1.B);
            Assert.Equal((byte)Math.Round(.864f * 255), color1.A);

            Rgba32 color2 = new Rgba32(1, .1f, .133f);
            Assert.Equal(255, color2.R);
            Assert.Equal(Math.Round(.1f * 255), color2.G);
            Assert.Equal(Math.Round(.133f * 255), color2.B);
            Assert.Equal(255, color2.A);

            Rgba32 color4 = new Rgba32(new Vector3(1, .1f, .133f));
            Assert.Equal(255, color4.R);
            Assert.Equal(Math.Round(.1f * 255), color4.G);
            Assert.Equal(Math.Round(.133f * 255), color4.B);
            Assert.Equal(255, color4.A);

            Rgba32 color5 = new Rgba32(new Vector4(1, .1f, .133f, .5f));
            Assert.Equal(255, color5.R);
            Assert.Equal(Math.Round(.1f * 255), color5.G);
            Assert.Equal(Math.Round(.133f * 255), color5.B);
            Assert.Equal(Math.Round(.5f * 255), color5.A);
        }

        /// <summary>
        /// Tests whether FromHex and ToHex work correctly.
        /// </summary>
        [Fact]
        public void FromAndToHex()
        {
            Rgba32 color = Rgba32.FromHex("#AABBCCDD");
            Assert.Equal(170, color.R);
            Assert.Equal(187, color.G);
            Assert.Equal(204, color.B);
            Assert.Equal(221, color.A);

            color.A = 170;
            color.B = 187;
            color.G = 204;
            color.R = 221;

            Assert.Equal("DDCCBBAA", color.ToHex());

            color.R = 0;

            Assert.Equal("00CCBBAA", color.ToHex());

            color.A = 255;

            Assert.Equal("00CCBBFF", color.ToHex());
        }

        /// <summary>
        /// Tests that the individual byte elements are layed out in RGBA order.
        /// </summary>
        [Fact]
        public unsafe void ByteLayout()
        {
            Rgba32 color = new Rgba32(1, 2, 3, 4);
            byte* colorBase = (byte*)&color;
            Assert.Equal(1, colorBase[0]);
            Assert.Equal(2, colorBase[1]);
            Assert.Equal(3, colorBase[2]);
            Assert.Equal(4, colorBase[3]);

            Assert.Equal(4, sizeof(Rgba32));
        }
    }
}