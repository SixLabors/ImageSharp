// <copyright file="ColorPackingTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Colors
{
    using System.Collections.Generic;
    using System.Numerics;
    using Xunit;

    public class ColorPackingTests
    {
        public static IEnumerable<object[]> Vector4PackData
        {
            get
            {
                var vector4Values = new Vector4[]
                    {
                        Vector4.Zero,
                        Vector4.One,
                        Vector4.UnitX,
                        Vector4.UnitY,
                        Vector4.UnitZ,
                        Vector4.UnitW,
                    };

                foreach (var vector4 in vector4Values)
                {
                    yield return new object[] { new Argb(), vector4 };
                    yield return new object[] { new Bgra4444(), vector4 };
                    yield return new object[] { new Bgra5551(), vector4 };
                    yield return new object[] { new Byte4(), vector4 };
                    yield return new object[] { new HalfVector4(), vector4 };
                    yield return new object[] { new NormalizedByte4(), vector4 };
                    yield return new object[] { new NormalizedShort4(), vector4 };
                    yield return new object[] { new Rgba1010102(), vector4 };
                    yield return new object[] { new Rgba64(), vector4 };
                    yield return new object[] { new Short4(), vector4 };
                }
            }
        }

        public static IEnumerable<object[]> Vector3PackData
        {
            get
            {
                var vector4Values = new Vector4[]
                    {
                        Vector4.One,
                        new Vector4(0, 0, 0, 1),
                        new Vector4(1, 0, 0, 1),
                        new Vector4(0, 1, 0, 1),
                        new Vector4(0, 0, 1, 1),
                    };

                foreach (var vector4 in vector4Values)
                {
                    yield return new object[] { new Argb(), vector4 };
                    yield return new object[] { new Bgr565(), vector4 };
                }
            }
        }

        [Theory]
        [MemberData(nameof(Vector4PackData))]
        [MemberData(nameof(Vector3PackData))]
        public void FromVector4ToVector4(IPackedVector packedVector, Vector4 vector4ToPack)
        {
            // Arrange
            var precision = 2;
            packedVector.PackFromVector4(vector4ToPack);

            // Act
            var vector4 = packedVector.ToVector4();

            // Assert
            Assert.Equal(vector4ToPack.X, vector4.X, precision);
            Assert.Equal(vector4ToPack.Y, vector4.Y, precision);
            Assert.Equal(vector4ToPack.Z, vector4.Z, precision);
            Assert.Equal(vector4ToPack.W, vector4.W, precision);
        }
    }
}
