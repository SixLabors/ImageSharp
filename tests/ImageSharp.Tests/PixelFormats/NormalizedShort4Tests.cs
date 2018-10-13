// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class NormalizedShort4Tests
    {
        [Fact]
        public void NormalizedShort4_PackedValues()
        {
            Assert.Equal(0xa6674000d99a0ccd, new NormalizedShort4(0.1f, -0.3f, 0.5f, -0.7f).PackedValue);
            Assert.Equal((ulong)4150390751449251866, new NormalizedShort4(0.0008f, 0.15f, 0.30f, 0.45f).PackedValue);
            Assert.Equal((ulong)0x0, new NormalizedShort4(Vector4.Zero).PackedValue);
            Assert.Equal((ulong)0x7FFF7FFF7FFF7FFF, new NormalizedShort4(Vector4.One).PackedValue);
            Assert.Equal(0x8001800180018001, new NormalizedShort4(-Vector4.One).PackedValue);
        }

        [Fact]
        public void NormalizedShort4_ToVector4()
        {
            // Test ToVector4
            Assert.Equal(Vector4.One, new NormalizedShort4(Vector4.One).ToVector4());
            Assert.Equal(Vector4.Zero, new NormalizedShort4(Vector4.Zero).ToVector4());
            Assert.Equal(-Vector4.One, new NormalizedShort4(-Vector4.One).ToVector4());
            Assert.Equal(Vector4.One, new NormalizedShort4(Vector4.One * 1234.0f).ToVector4());
            Assert.Equal(-Vector4.One, new NormalizedShort4(Vector4.One * -1234.0f).ToVector4());
        }

        [Fact]
        public void NormalizedShort4_ToScaledVector4()
        {
            // arrange
            var short4 = new NormalizedShort4(Vector4.One);

            // act
            Vector4 actual = short4.ToScaledVector4();

            // assert
            Assert.Equal(1, actual.X);
            Assert.Equal(1, actual.Y);
            Assert.Equal(1, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void NormalizedShort4_PackFromScaledVector4()
        {
            // arrange
            var pixel = default(NormalizedShort4);
            Vector4 scaled = new NormalizedShort4(Vector4.One).ToScaledVector4();
            ulong expected = 0x7FFF7FFF7FFF7FFF;

            // act 
            pixel.PackFromScaledVector4(scaled);
            ulong actual = pixel.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }
    }
}
