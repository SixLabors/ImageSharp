// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class Bgra5551Tests
    {
        [Fact]
        public void Bgra5551_PackedValue()
        {
            float x = 0x1a;
            float y = 0x16;
            float z = 0xd;
            float w = 0x1;
            Assert.Equal(0xeacd, new Bgra5551(x / 0x1f, y / 0x1f, z / 0x1f, w).PackedValue);
            Assert.Equal(3088, new Bgra5551(0.1f, -0.3f, 0.5f, -0.7f).PackedValue);
            // Test the limits.
            Assert.Equal(0x0, new Bgra5551(Vector4.Zero).PackedValue);
            Assert.Equal(0xFFFF, new Bgra5551(Vector4.One).PackedValue);
        }

        [Fact]
        public void Bgra5551_ToVector4()
        {
            Assert.Equal(Vector4.Zero, new Bgra5551(Vector4.Zero).ToVector4());
            Assert.Equal(Vector4.One, new Bgra5551(Vector4.One).ToVector4());
        }

        [Fact]
        public void Bgra5551_ToScaledVector4()
        {
            // arrange
            var bgra = new Bgra5551(Vector4.One);

            // act 
            Vector4 actual = bgra.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(1, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Bgra5551_FromScaledVector4()
        {
            // arrange
            Vector4 scaled = new Bgra5551(Vector4.One).ToScaledVector4();
            int expected = 0xFFFF;
            var pixel = default(Bgra5551);
            pixel.FromScaledVector4(scaled);

            // act
            pixel.FromScaledVector4(scaled);
            ushort actual = pixel.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra5551_Clamping()
        {
            Assert.Equal(Vector4.Zero, new Bgra5551(Vector4.One * -1234.0f).ToVector4());
            Assert.Equal(Vector4.One, new Bgra5551(Vector4.One * 1234.0f).ToVector4());
        }
    }
}
