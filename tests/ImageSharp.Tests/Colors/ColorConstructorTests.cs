// <copyright file="ColorConstructorTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Colors
{
    using System.Numerics;
    using Xunit;

    public class ColorConstructorTests
    {
        public static readonly TheoryData<object, Vector4> ColorData =
            new TheoryData<object, Vector4>()
            {
                { new Alpha8(.5F), new Vector4(0, 0, 0, .5F) },
                { new Argb(Vector4.One), Vector4.One },
                { new Argb(Vector4.Zero), Vector4.Zero },
                { new Argb(Vector4.UnitX), Vector4.UnitX },
                { new Argb(Vector4.UnitY), Vector4.UnitY },
                { new Argb(Vector4.UnitZ), Vector4.UnitZ },
                { new Argb(Vector4.UnitW), Vector4.UnitW },
            };

        [Theory]
        [MemberData(nameof(ColorData))]
        public void ConstructorToVector4(IPackedVector color, Vector4 expectedVector4)
        {
            // Arrange
            var precision = 2;

            // Act
            var vector4 = color.ToVector4();

            // Assert
            Assert.Equal(expectedVector4.X, vector4.X, precision);
            Assert.Equal(expectedVector4.Y, vector4.Y, precision);
            Assert.Equal(expectedVector4.Z, vector4.Z, precision);
            Assert.Equal(expectedVector4.W, vector4.W, precision);
        }
    }
}
