// <copyright file="ColorEqualityTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Colors
{
    using System.Numerics;
    using Xunit;

    /// <summary>
    /// Test implementations of IEquatable
    /// </summary>
    public class ColorEqualityTests
    {
        public static readonly TheoryData<object, object> EqualityData =
           new TheoryData<object, object>()
           {
               { new Alpha8(.5F), new Alpha8(.5F) },
               { new Argb(Vector4.One), new Argb(Vector4.One) },
               { new Bgr565(Vector3.One), new Bgr565(Vector3.One) },
               { new Bgra4444(Vector4.One), new Bgra4444(Vector4.One) },
               { new Bgra5551(Vector4.One), new Bgra5551(Vector4.One) },
               { new Byte4(Vector4.One * 255), new Byte4(Vector4.One * 255) },
               { new HalfSingle(-1F), new HalfSingle(-1F) },
               { new HalfVector2(0.1f, -0.3f), new HalfVector2(0.1f, -0.3f) },
               { new HalfVector4(Vector4.One), new HalfVector4(Vector4.One) },
               { new NormalizedByte2(-Vector2.One), new NormalizedByte2(-Vector2.One) },
               { new NormalizedByte4(Vector4.One), new NormalizedByte4(Vector4.One) },
               { new NormalizedShort2(Vector2.One), new NormalizedShort2(Vector2.One) },
               { new NormalizedShort4(Vector4.One), new NormalizedShort4(Vector4.One) },
               { new Rg32(Vector2.One), new Rg32(Vector2.One) },
               { new Rgba1010102(Vector4.One), new Rgba1010102(Vector4.One) },
               { new Rgba64(Vector4.One), new Rgba64(Vector4.One) },
               { new Short2(Vector2.One * 0x7FFF), new Short2(Vector2.One * 0x7FFF) },
               { new Short4(Vector4.One * 0x7FFF), new Short4(Vector4.One * 0x7FFF) },
           };

        [Theory]
        [MemberData("EqualityData")]
        public void EqualsOperator(object first, object second)
        {
            // Act
            var equal = first.Equals(second);

            // Assert
            Assert.True(equal);
        }

    }
}
