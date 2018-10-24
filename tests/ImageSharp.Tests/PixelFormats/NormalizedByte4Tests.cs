// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    public class NormalizedByte4Tests
    {
        [Fact]
        public void NormalizedByte4_PackedValues()
        {
            Assert.Equal(0xA740DA0D, new NormalizedByte4(0.1f, -0.3f, 0.5f, -0.7f).PackedValue);
            Assert.Equal((uint)958796544, new NormalizedByte4(0.0008f, 0.15f, 0.30f, 0.45f).PackedValue);
            Assert.Equal((uint)0x0, new NormalizedByte4(Vector4.Zero).PackedValue);
            Assert.Equal((uint)0x7F7F7F7F, new NormalizedByte4(Vector4.One).PackedValue);
            Assert.Equal(0x81818181, new NormalizedByte4(-Vector4.One).PackedValue);
        }

        [Fact]
        public void NormalizedByte4_ToVector4()
        {
            Assert.Equal(Vector4.One, new NormalizedByte4(Vector4.One).ToVector4());
            Assert.Equal(Vector4.Zero, new NormalizedByte4(Vector4.Zero).ToVector4());
            Assert.Equal(-Vector4.One, new NormalizedByte4(-Vector4.One).ToVector4());
            Assert.Equal(Vector4.One, new NormalizedByte4(Vector4.One * 1234.0f).ToVector4());
            Assert.Equal(-Vector4.One, new NormalizedByte4(Vector4.One * -1234.0f).ToVector4());
        }

        [Fact]
        public void NormalizedByte4_ToScaledVector4()
        {
            // arrange
            var short4 = new NormalizedByte4(-Vector4.One);

            // act
            Vector4 actual = short4.ToScaledVector4();

            // assert
            Assert.Equal(0, actual.X);
            Assert.Equal(0, actual.Y);
            Assert.Equal(0, actual.Z);
            Assert.Equal(0, actual.W);
        }

        [Fact]
        public void NormalizedByte4_FromScaledVector4()
        {
            // arrange
            var pixel = default(NormalizedByte4);
            Vector4 scaled = new NormalizedByte4(-Vector4.One).ToScaledVector4();
            uint expected = 0x81818181;

            // act 
            pixel.FromScaledVector4(scaled);
            uint actual = pixel.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }
    }
}
