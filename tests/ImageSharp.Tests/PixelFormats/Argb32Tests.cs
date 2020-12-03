// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    [Trait("Category", "PixelFormats")]
    public class Argb32Tests
    {
        /// <summary>
        /// Tests the equality operators for equality.
        /// </summary>
        [Fact]
        public void AreEqual()
        {
            var color1 = new Argb32(0.0f, 0.0f, 0.0f, 0.0f);
            var color2 = new Argb32(new Vector4(0.0f));
            var color3 = new Argb32(new Vector4(1.0f, 0.0f, 1.0f, 1.0f));
            var color4 = new Argb32(1.0f, 0.0f, 1.0f, 1.0f);

            Assert.Equal(color1, color2);
            Assert.Equal(color3, color4);
        }

        /// <summary>
        /// Tests the equality operators for inequality.
        /// </summary>
        [Fact]
        public void AreNotEqual()
        {
            var color1 = new Argb32(0.0f, 0.0f, 0.0f, 0.0f);
            var color2 = new Argb32(new Vector4(1.0f));
            var color3 = new Argb32(new Vector4(1.0f, 0.0f, 0.0f, 1.0f));
            var color4 = new Argb32(1.0f, 1.0f, 0.0f, 1.0f);

            Assert.NotEqual(color1, color2);
            Assert.NotEqual(color3, color4);
        }

        /// <summary>
        /// Tests whether the color constructor correctly assign properties.
        /// </summary>
        [Fact]
        public void ConstructorAssignsProperties()
        {
            var color1 = new Argb32(1, .1f, .133f, .864f);
            Assert.Equal(255, color1.R);
            Assert.Equal((byte)Math.Round(.1f * 255), color1.G);
            Assert.Equal((byte)Math.Round(.133f * 255), color1.B);
            Assert.Equal((byte)Math.Round(.864f * 255), color1.A);

            var color2 = new Argb32(1, .1f, .133f);
            Assert.Equal(255, color2.R);
            Assert.Equal(Math.Round(.1f * 255), color2.G);
            Assert.Equal(Math.Round(.133f * 255), color2.B);
            Assert.Equal(255, color2.A);

            var color4 = new Argb32(new Vector3(1, .1f, .133f));
            Assert.Equal(255, color4.R);
            Assert.Equal(Math.Round(.1f * 255), color4.G);
            Assert.Equal(Math.Round(.133f * 255), color4.B);
            Assert.Equal(255, color4.A);

            var color5 = new Argb32(new Vector4(1, .1f, .133f, .5f));
            Assert.Equal(255, color5.R);
            Assert.Equal(Math.Round(.1f * 255), color5.G);
            Assert.Equal(Math.Round(.133f * 255), color5.B);
            Assert.Equal(Math.Round(.5f * 255), color5.A);
        }

        [Fact]
        public void Argb32_PackedValue()
        {
            Assert.Equal(0x80001a00u, new Argb32(+0.1f, -0.3f, +0.5f, -0.7f).PackedValue);
            Assert.Equal(0x0U, new Argb32(Vector4.Zero).PackedValue);
            Assert.Equal(0xFFFFFFFF, new Argb32(Vector4.One).PackedValue);
        }

        [Fact]
        public void Argb32_ToVector4()
        {
            Assert.Equal(Vector4.One, new Argb32(Vector4.One).ToVector4());
            Assert.Equal(Vector4.Zero, new Argb32(Vector4.Zero).ToVector4());
            Assert.Equal(Vector4.UnitX, new Argb32(Vector4.UnitX).ToVector4());
            Assert.Equal(Vector4.UnitY, new Argb32(Vector4.UnitY).ToVector4());
            Assert.Equal(Vector4.UnitZ, new Argb32(Vector4.UnitZ).ToVector4());
            Assert.Equal(Vector4.UnitW, new Argb32(Vector4.UnitW).ToVector4());
        }

        [Fact]
        public void Argb32_ToScaledVector4()
        {
            // arrange
            var argb = new Argb32(Vector4.One);

            // act
            Vector4 actual = argb.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(1, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Argb32_FromScaledVector4()
        {
            // arrange
            Vector4 scaled = new Argb32(Vector4.One).ToScaledVector4();
            var pixel = default(Argb32);
            uint expected = 0xFFFFFFFF;

            // act
            pixel.FromScaledVector4(scaled);
            uint actual = pixel.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Argb32_FromBgra5551()
        {
            // arrange
            var argb = default(Argb32);
            uint expected = uint.MaxValue;

            // act
            argb.FromBgra5551(new Bgra5551(1.0f, 1.0f, 1.0f, 1.0f));

            // assert
            Assert.Equal(expected, argb.PackedValue);
        }

        [Fact]
        public void Argb32_Clamping()
        {
            Assert.Equal(Vector4.Zero, new Argb32(Vector4.One * -1234.0f).ToVector4());
            Assert.Equal(Vector4.One, new Argb32(Vector4.One * +1234.0f).ToVector4());
        }
    }
}
