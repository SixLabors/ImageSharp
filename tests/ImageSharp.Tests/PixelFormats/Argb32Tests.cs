// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class Argb32Tests
    {
        [Fact]
        public void Argb32_PackedValue()
        {
            Assert.Equal(0x80001a00u, new Argb32(+0.1f, -0.3f, +0.5f, -0.7f).PackedValue);
            Assert.Equal((uint)0x0, new Argb32(Vector4.Zero).PackedValue);
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
        public void Argb32_PackFromScaledVector4()
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
        public void Argb32_Clamping()
        {
            Assert.Equal(Vector4.Zero, new Argb32(Vector4.One * -1234.0f).ToVector4());
            Assert.Equal(Vector4.One, new Argb32(Vector4.One * +1234.0f).ToVector4());
        }
    }
}
