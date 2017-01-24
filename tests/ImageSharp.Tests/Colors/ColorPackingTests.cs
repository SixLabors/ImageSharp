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
                    var vector4Components = new float[] { vector4.X, vector4.Y, vector4.Z, vector4.W };

                    yield return new object[] { new Argb(), vector4Components };
                    yield return new object[] { new Bgra4444(), vector4Components };
                    yield return new object[] { new Bgra5551(), vector4Components };
                    yield return new object[] { new Byte4(), vector4Components };
                    yield return new object[] { new HalfVector4(), vector4Components };
                    yield return new object[] { new NormalizedByte4(), vector4Components };
                    yield return new object[] { new NormalizedShort4(), vector4Components };
                    yield return new object[] { new Rgba1010102(), vector4Components };
                    yield return new object[] { new Rgba64(), vector4Components };
                    yield return new object[] { new Short4(), vector4Components };
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
                    var vector4Components = new float[] { vector4.X, vector4.Y, vector4.Z, vector4.W };

                    yield return new object[] { new Argb(), vector4Components };
                    yield return new object[] { new Bgr565(), vector4Components };
                }
            }
        }

        [Theory]
        [MemberData(nameof(Vector4PackData))]
        [MemberData(nameof(Vector3PackData))]
        public void FromVector4ToVector4(IPackedVector packedVector, float[] vector4ComponentsToPack)
        {
            // Arrange
            var precision = 2;
            var vector4ToPack = new Vector4(vector4ComponentsToPack[0], vector4ComponentsToPack[1], vector4ComponentsToPack[2], vector4ComponentsToPack[3]);
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
