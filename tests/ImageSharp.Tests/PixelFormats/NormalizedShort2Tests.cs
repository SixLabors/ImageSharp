// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    [Trait("Category", "PixelFormats")]
    public class NormalizedShort2Tests
    {
        [Fact]
        public void NormalizedShort2_PackedValue()
        {
            Assert.Equal(0xE6672CCC, new NormalizedShort2(0.35f, -0.2f).PackedValue);
            Assert.Equal(3650751693, new NormalizedShort2(0.1f, -0.3f).PackedValue);
            Assert.Equal(0x0U, new NormalizedShort2(Vector2.Zero).PackedValue);
            Assert.Equal(0x7FFF7FFFU, new NormalizedShort2(Vector2.One).PackedValue);
            Assert.Equal(0x80018001, new NormalizedShort2(-Vector2.One).PackedValue);

            // TODO: I don't think this can ever pass since the bytes are already truncated.
            // Assert.Equal(3650751693, n.PackedValue);
        }

        [Fact]
        public void NormalizedShort2_ToVector2()
        {
            Assert.Equal(Vector2.One, new NormalizedShort2(Vector2.One).ToVector2());
            Assert.Equal(Vector2.Zero, new NormalizedShort2(Vector2.Zero).ToVector2());
            Assert.Equal(-Vector2.One, new NormalizedShort2(-Vector2.One).ToVector2());
            Assert.Equal(Vector2.One, new NormalizedShort2(Vector2.One * 1234.0f).ToVector2());
            Assert.Equal(-Vector2.One, new NormalizedShort2(Vector2.One * -1234.0f).ToVector2());
        }

        [Fact]
        public void NormalizedShort2_ToVector4()
        {
            Assert.Equal(new Vector4(1, 1, 0, 1), new NormalizedShort2(Vector2.One).ToVector4());
            Assert.Equal(new Vector4(0, 0, 0, 1), new NormalizedShort2(Vector2.Zero).ToVector4());
        }

        [Fact]
        public void NormalizedShort2_ToScaledVector4()
        {
            // arrange
            var short2 = new NormalizedShort2(-Vector2.One);

            // act
            Vector4 actual = short2.ToScaledVector4();

            // assert
            Assert.Equal(0, actual.X);
            Assert.Equal(0, actual.Y);
            Assert.Equal(0, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void NormalizedShort2_FromScaledVector4()
        {
            // arrange
            Vector4 scaled = new NormalizedShort2(-Vector2.One).ToScaledVector4();
            var short2 = default(NormalizedShort2);
            uint expected = 0x80018001;

            // act
            short2.FromScaledVector4(scaled);
            uint actual = short2.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void NormalizedShort2_FromBgra5551()
        {
            // arrange
            var normalizedShort2 = default(NormalizedShort2);
            var expected = new Vector4(1, 1, 0, 1);

            // act
            normalizedShort2.FromBgra5551(new Bgra5551(1.0f, 1.0f, 1.0f, 1.0f));

            // assert
            Assert.Equal(expected, normalizedShort2.ToVector4());
        }
    }
}
