// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class Bgr565Tests
    {
        [Fact]
        public void Bgr565_PackedValue()
        {
            Assert.Equal(6160, new Bgr565(0.1F, -0.3F, 0.5F).PackedValue);
            Assert.Equal(0x0, new Bgr565(Vector3.Zero).PackedValue);
            Assert.Equal(0xFFFF, new Bgr565(Vector3.One).PackedValue);
            // Make sure the swizzle is correct.
            Assert.Equal(0xF800, new Bgr565(Vector3.UnitX).PackedValue);
            Assert.Equal(0x07E0, new Bgr565(Vector3.UnitY).PackedValue);
            Assert.Equal(0x001F, new Bgr565(Vector3.UnitZ).PackedValue);
        }

        [Fact]
        public void Bgr565_ToVector3()
        {
            Assert.Equal(Vector3.One, new Bgr565(Vector3.One).ToVector3());
            Assert.Equal(Vector3.Zero, new Bgr565(Vector3.Zero).ToVector3());
            Assert.Equal(Vector3.UnitX, new Bgr565(Vector3.UnitX).ToVector3());
            Assert.Equal(Vector3.UnitY, new Bgr565(Vector3.UnitY).ToVector3());
            Assert.Equal(Vector3.UnitZ, new Bgr565(Vector3.UnitZ).ToVector3());
        }

        [Fact]
        public void Bgr565_ToScaledVector4()
        {
            // arrange
            var bgr = new Bgr565(Vector3.One);

            // act
            Vector4 actual = bgr.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(1, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Bgr565_PackFromScaledVector4()
        {
            // arrange
            Vector4 scaled = new Bgr565(Vector3.One).ToScaledVector4();
            int expected = 0xFFFF;
            var pixel = default(Bgr565);

            // act
            pixel.FromScaledVector4(scaled);
            ushort actual = pixel.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgr565_Clamping()
        {
            Assert.Equal(Vector3.Zero, new Bgr565(Vector3.One * -1234F).ToVector3());
            Assert.Equal(Vector3.One, new Bgr565(Vector3.One * 1234F).ToVector3());
        }
    }
}
