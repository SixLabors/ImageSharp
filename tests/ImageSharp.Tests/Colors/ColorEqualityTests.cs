// <copyright file="ColorEqualityTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Colors
{
    using System;
    using System.Numerics;
    using Xunit;

    /// <summary>
    /// Test implementations of IEquatable
    /// </summary>
    public class ColorEqualityTests
    {
        public static readonly TheoryData<object, object, Type> EqualityData =
           new TheoryData<object, object, Type>()
           {
               { new Alpha8(.5F), new Alpha8(.5F), typeof(Alpha8) },
               { new Argb(Vector4.One), new Argb(Vector4.One), typeof(Argb) },
               { new Bgr565(Vector3.One), new Bgr565(Vector3.One), typeof(Bgr565) },
               { new Bgra4444(Vector4.One), new Bgra4444(Vector4.One), typeof(Bgra4444) },
               { new Bgra5551(Vector4.One), new Bgra5551(Vector4.One), typeof(Bgra5551) },
               { new Byte4(Vector4.One * 255), new Byte4(Vector4.One * 255), typeof(Byte4) },
               { new HalfSingle(-1F), new HalfSingle(-1F), typeof(HalfSingle) },
               { new HalfVector2(0.1f, -0.3f), new HalfVector2(0.1f, -0.3f), typeof(HalfVector2) },
               { new HalfVector4(Vector4.One), new HalfVector4(Vector4.One), typeof(HalfVector4) },
               { new NormalizedByte2(-Vector2.One), new NormalizedByte2(-Vector2.One), typeof(NormalizedByte2) },
               { new NormalizedByte4(Vector4.One), new NormalizedByte4(Vector4.One), typeof(NormalizedByte4) },
               { new NormalizedShort2(Vector2.One), new NormalizedShort2(Vector2.One), typeof(NormalizedShort2) },
               { new NormalizedShort4(Vector4.One), new NormalizedShort4(Vector4.One), typeof(NormalizedShort4) },
               { new Rg32(Vector2.One), new Rg32(Vector2.One), typeof(Rg32) },
               { new Rgba1010102(Vector4.One), new Rgba1010102(Vector4.One), typeof(Rgba1010102) },
               { new Rgba64(Vector4.One), new Rgba64(Vector4.One), typeof(Rgba64) },
               { new Short2(Vector2.One * 0x7FFF), new Short2(Vector2.One * 0x7FFF), typeof(Short2) },
               { new Short4(Vector4.One * 0x7FFF), new Short4(Vector4.One * 0x7FFF), typeof(Short4) },
           };

        public static readonly TheoryData<object, object> NotEqualityDataNulls =
            new TheoryData<object, object>()
            {
                // Valid object against null
                { new Alpha8(.5F), null },
                { new Argb(Vector4.One), null },
                { new Bgr565(Vector3.One), null },
                { new Bgra4444(Vector4.One), null },
                { new Bgra5551(Vector4.One), null },
                { new Byte4(Vector4.One * 255), null },
                { new HalfSingle(-1F), null },
                { new HalfVector2(0.1f, -0.3f), null },
                { new HalfVector4(Vector4.One), null },
                { new NormalizedByte2(-Vector2.One), null },
                { new NormalizedByte4(Vector4.One), null },
                { new NormalizedShort2(Vector2.One), null },
                { new NormalizedShort4(Vector4.One), null },
                { new Rg32(Vector2.One), null },
                { new Rgba1010102(Vector4.One), null },
                { new Rgba64(Vector4.One), null },
                { new Short2(Vector2.One * 0x7FFF), null },
                { new Short4(Vector4.One * 0x7FFF), null },
            };

        public static readonly TheoryData<object, object> NotEqualityDataDifferentObjects =
           new TheoryData<object, object>()
           {
                // Valid objects of different types but not equal
                { new Alpha8(.5F), new Argb(Vector4.Zero) },
                { new HalfSingle(-1F), new NormalizedShort2(Vector2.Zero) },
                { new Rgba1010102(Vector4.One), new Bgra5551(Vector4.Zero) },
           };

        public static readonly TheoryData<object, object> NotEqualityData =
           new TheoryData<object, object>()
           {
                // Valid objects of the same type but not equal
                { new Alpha8(.5F), new Alpha8(.8F) },
                { new Argb(Vector4.One), new Argb(Vector4.Zero) },
                { new Bgr565(Vector3.One), new Bgr565(Vector3.Zero) },
                { new Bgra4444(Vector4.One), new Bgra4444(Vector4.Zero) },
                { new Bgra5551(Vector4.One), new Bgra5551(Vector4.Zero) },
                { new Byte4(Vector4.One * 255), new Byte4(Vector4.Zero) },
                { new HalfSingle(-1F), new HalfSingle(1F) },
                { new HalfVector2(0.1f, -0.3f), new HalfVector2(0.1f, 0.3f) },
                { new HalfVector4(Vector4.One), new HalfVector4(Vector4.Zero) },
                { new NormalizedByte2(-Vector2.One), new NormalizedByte2(-Vector2.Zero) },
                { new NormalizedByte4(Vector4.One), new NormalizedByte4(Vector4.Zero) },
                { new NormalizedShort2(Vector2.One), new NormalizedShort2(Vector2.Zero) },
                { new NormalizedShort4(Vector4.One), new NormalizedShort4(Vector4.Zero) },
                { new Rg32(Vector2.One), new Rg32(Vector2.Zero) },
                { new Rgba1010102(Vector4.One), new Rgba1010102(Vector4.Zero) },
                { new Rgba64(Vector4.One), new Rgba64(Vector4.Zero) },
                { new Short2(Vector2.One * 0x7FFF), new Short2(Vector2.Zero) },
                { new Short4(Vector4.One * 0x7FFF), new Short4(Vector4.Zero) },
           };

        [Theory]
        [MemberData(nameof(EqualityData))]
        public void Equality(object first, object second, Type type)
        {
            // Act
            var equal = first.Equals(second);

            // Assert
            Assert.True(equal);
        }

        [Theory]
        [MemberData(nameof(NotEqualityDataNulls))]
        [MemberData(nameof(NotEqualityDataDifferentObjects))]
        [MemberData(nameof(NotEqualityData))]
        public void NotEquality(object first, object second)
        {
            // Act
            var equal = first.Equals(second);

            // Assert
            Assert.False(equal);
        }

        [Theory]
        [MemberData(nameof(EqualityData))]
        public void HashCodeEqual(object first, object second, Type type)
        {
            // Act
            var equal = first.GetHashCode() == second.GetHashCode();

            // Assert
            Assert.True(equal);
        }

        [Theory]
        [MemberData(nameof(NotEqualityDataDifferentObjects))]
        public void HashCodeNotEqual(object first, object second)
        {
            // Act
            var equal = first.GetHashCode() == second.GetHashCode();

            // Assert
            Assert.False(equal);
        }

        [Theory]
        [MemberData(nameof(EqualityData))]
        public void EqualityOperator(object first, object second, Type type)
        {
            // Arrange 
            // Cast to the known object types 

            // Act
            var equal = first.Equals(second);

            // Assert
            Assert.True(equal);
        }
    }
}
