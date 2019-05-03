// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class Bgra4444Tests
    {
        [Fact]
        public void Bgra4444_PackedValue()
        {
            Assert.Equal(520, new Bgra4444(0.1f, -0.3f, 0.5f, -0.7f).PackedValue);
            Assert.Equal(0x0, new Bgra4444(Vector4.Zero).PackedValue);
            Assert.Equal(0xFFFF, new Bgra4444(Vector4.One).PackedValue);
            Assert.Equal(0x0F00, new Bgra4444(Vector4.UnitX).PackedValue);
            Assert.Equal(0x00F0, new Bgra4444(Vector4.UnitY).PackedValue);
            Assert.Equal(0x000F, new Bgra4444(Vector4.UnitZ).PackedValue);
            Assert.Equal(0xF000, new Bgra4444(Vector4.UnitW).PackedValue);
        }

        [Fact]
        public void Bgra4444_ToVector4()
        {
            Assert.Equal(Vector4.One, new Bgra4444(Vector4.One).ToVector4());
            Assert.Equal(Vector4.Zero, new Bgra4444(Vector4.Zero).ToVector4());
            Assert.Equal(Vector4.UnitX, new Bgra4444(Vector4.UnitX).ToVector4());
            Assert.Equal(Vector4.UnitY, new Bgra4444(Vector4.UnitY).ToVector4());
            Assert.Equal(Vector4.UnitZ, new Bgra4444(Vector4.UnitZ).ToVector4());
            Assert.Equal(Vector4.UnitW, new Bgra4444(Vector4.UnitW).ToVector4());
        }

        [Fact]
        public void Bgra4444_ToScaledVector4()
        {
            // arrange
            var bgra = new Bgra4444(Vector4.One);

            // act
            Vector4 actual = bgra.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(1, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void Bgra4444_FromScaledVector4()
        {
            // arrange
            Vector4 scaled = new Bgra4444(Vector4.One).ToScaledVector4();
            int expected = 0xFFFF;
            var bgra = default(Bgra4444);

            // act
            bgra.FromScaledVector4(scaled);
            ushort actual = bgra.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void Bgra4444_FromBgra5551()
        {
            // arrange
            var bgra = default(Bgra4444);
            ushort expected = ushort.MaxValue;

            // act
            bgra.FromBgra5551(new Bgra5551(1.0f, 1.0f, 1.0f, 1.0f));

            // assert
            Assert.Equal(expected, bgra.PackedValue);
        }

        [Fact]
        public void Bgra4444_Clamping()
        {
            Assert.Equal(Vector4.Zero, new Bgra4444(Vector4.One * -1234.0f).ToVector4());
            Assert.Equal(Vector4.One, new Bgra4444(Vector4.One * 1234.0f).ToVector4());
        }
    }
}
