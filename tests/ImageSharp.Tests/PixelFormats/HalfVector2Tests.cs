// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.PixelFormats
{
    [Trait("Category", "PixelFormats")]
    public class HalfVector2Tests
    {
        [Fact]
        public void HalfVector2_PackedValue()
        {
            Assert.Equal(0u, new HalfVector2(Vector2.Zero).PackedValue);
            Assert.Equal(1006648320u, new HalfVector2(Vector2.One).PackedValue);
            Assert.Equal(3033345638u, new HalfVector2(0.1f, -0.3f).PackedValue);
        }

        [Fact]
        public void HalfVector2_ToVector2()
        {
            Assert.Equal(Vector2.Zero, new HalfVector2(Vector2.Zero).ToVector2());
            Assert.Equal(Vector2.One, new HalfVector2(Vector2.One).ToVector2());
            Assert.Equal(Vector2.UnitX, new HalfVector2(Vector2.UnitX).ToVector2());
            Assert.Equal(Vector2.UnitY, new HalfVector2(Vector2.UnitY).ToVector2());
        }

        [Fact]
        public void HalfVector2_ToScaledVector4()
        {
            // arrange
            var halfVector = new HalfVector2(Vector2.One);

            // act
            Vector4 actual = halfVector.ToScaledVector4();

            // assert
            Assert.Equal(1F, actual.X);
            Assert.Equal(1F, actual.Y);
            Assert.Equal(0, actual.Z);
            Assert.Equal(1, actual.W);
        }

        [Fact]
        public void HalfVector2_FromScaledVector4()
        {
            // arrange
            Vector4 scaled = new HalfVector2(Vector2.One).ToScaledVector4();
            uint expected = 1006648320u;
            var halfVector = default(HalfVector2);

            // act
            halfVector.FromScaledVector4(scaled);
            uint actual = halfVector.PackedValue;

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector2_ToVector4()
        {
            // arrange
            var halfVector = new HalfVector2(.5F, .25F);
            var expected = new Vector4(0.5f, .25F, 0, 1);

            // act
            var actual = halfVector.ToVector4();

            // assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void HalfVector2_FromBgra5551()
        {
            // arrange
            var halfVector2 = default(HalfVector2);

            // act
            halfVector2.FromBgra5551(new Bgra5551(1.0f, 1.0f, 1.0f, 1.0f));

            // assert
            Vector4 actual = halfVector2.ToScaledVector4();
            Assert.Equal(1F, actual.X);
            Assert.Equal(1F, actual.Y);
            Assert.Equal(0, actual.Z);
            Assert.Equal(1, actual.W);
        }
    }
}
