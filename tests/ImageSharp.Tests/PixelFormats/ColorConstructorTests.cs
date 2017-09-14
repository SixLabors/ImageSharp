// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colors
{
    public class ColorConstructorTests
    {
        public static IEnumerable<object[]> Vector4Data
        {
            get
            {
                Vector4[] vector4Values = new Vector4[]
                    {
                        Vector4.Zero,
                        Vector4.One,
                        Vector4.UnitX,
                        Vector4.UnitY,
                        Vector4.UnitZ,
                        Vector4.UnitW,
                    };

                foreach (Vector4 vector4 in vector4Values)
                {
                    // using float array to work around a bug in xunit corruptint the state of any Vector4 passed as MemberData
                    float[] vector4Components = new float[] { vector4.X, vector4.Y, vector4.Z, vector4.W };

                    yield return new object[] { new Argb32(vector4), vector4Components };
                    yield return new object[] { new Bgra4444(vector4), vector4Components };
                    yield return new object[] { new Bgra5551(vector4), vector4Components };
                    yield return new object[] { new Byte4(vector4), vector4Components };
                    yield return new object[] { new HalfVector4(vector4), vector4Components };
                    yield return new object[] { new NormalizedByte4(vector4), vector4Components };
                    yield return new object[] { new NormalizedShort4(vector4), vector4Components };
                    yield return new object[] { new Rgba1010102(vector4), vector4Components };
                    yield return new object[] { new Rgba64(vector4), vector4Components };
                    yield return new object[] { new Short4(vector4), vector4Components };
                }
            }
        }

        public static IEnumerable<object[]> Vector3Data
        {
            get
            {
                Dictionary<Vector3, Vector4> vector3Values = new Dictionary<Vector3, Vector4>()
                    {
                        { Vector3.One, Vector4.One },
                        { Vector3.Zero, new Vector4(0, 0, 0, 1) },
                        { Vector3.UnitX, new Vector4(1, 0, 0, 1) },
                        { Vector3.UnitY, new Vector4(0, 1, 0, 1) },
                        { Vector3.UnitZ, new Vector4(0, 0, 1, 1) },
                    };

                foreach (Vector3 vector3 in vector3Values.Keys)
                {
                    Vector4 vector4 = vector3Values[vector3];
                    // using float array to work around a bug in xunit corruptint the state of any Vector4 passed as MemberData
                    float[] vector4Components = new float[] { vector4.X, vector4.Y, vector4.Z, vector4.W };

                    yield return new object[] { new Argb32(vector3), vector4Components };
                    yield return new object[] { new Bgr565(vector3), vector4Components };
                }
            }
        }

        public static IEnumerable<object[]> Float4Data
        {
            get
            {
                Vector4[] vector4Values = new Vector4[]
                    {
                        Vector4.Zero,
                        Vector4.One,
                        Vector4.UnitX,
                        Vector4.UnitY,
                        Vector4.UnitZ,
                        Vector4.UnitW,
                    };

                foreach (Vector4 vector4 in vector4Values)
                {
                    // using float array to work around a bug in xunit corruptint the state of any Vector4 passed as MemberData
                    float[] vector4Components = new float[] { vector4.X, vector4.Y, vector4.Z, vector4.W };

                    yield return new object[] { new Argb32(vector4.X, vector4.Y, vector4.Z, vector4.W), vector4Components };
                    yield return new object[] { new Bgra4444(vector4.X, vector4.Y, vector4.Z, vector4.W), vector4Components };
                    yield return new object[] { new Bgra5551(vector4.X, vector4.Y, vector4.Z, vector4.W), vector4Components };
                    yield return new object[] { new Byte4(vector4.X, vector4.Y, vector4.Z, vector4.W), vector4Components };
                    yield return new object[] { new HalfVector4(vector4.X, vector4.Y, vector4.Z, vector4.W), vector4Components };
                    yield return new object[] { new NormalizedByte4(vector4.X, vector4.Y, vector4.Z, vector4.W), vector4Components };
                    yield return new object[] { new NormalizedShort4(vector4.X, vector4.Y, vector4.Z, vector4.W), vector4Components };
                    yield return new object[] { new Rgba1010102(vector4.X, vector4.Y, vector4.Z, vector4.W), vector4Components };
                    yield return new object[] { new Rgba64(vector4.X, vector4.Y, vector4.Z, vector4.W), vector4Components };
                    yield return new object[] { new Short4(vector4.X, vector4.Y, vector4.Z, vector4.W), vector4Components };
                }
            }
        }

        public static IEnumerable<object[]> Float3Data
        {
            get
            {
                Dictionary<Vector3, Vector4> vector3Values = new Dictionary<Vector3, Vector4>()
                    {
                        { Vector3.One, Vector4.One },
                        { Vector3.Zero, new Vector4(0, 0, 0, 1) },
                        { Vector3.UnitX, new Vector4(1, 0, 0, 1) },
                        { Vector3.UnitY, new Vector4(0, 1, 0, 1) },
                        { Vector3.UnitZ, new Vector4(0, 0, 1, 1) },
                    };

                foreach (Vector3 vector3 in vector3Values.Keys)
                {
                    Vector4 vector4 = vector3Values[vector3];
                    // using float array to work around a bug in xunit corruptint the state of any Vector4 passed as MemberData
                    float[] vector4Components = new float[] { vector4.X, vector4.Y, vector4.Z, vector4.W };

                    yield return new object[] { new Argb32(vector3.X, vector3.Y, vector3.Z), vector4Components };
                    yield return new object[] { new Bgr565(vector3.X, vector3.Y, vector3.Z), vector4Components };
                }
            }
        }

        [Theory]
        [MemberData(nameof(Vector4Data))]
        [MemberData(nameof(Vector3Data))]
        [MemberData(nameof(Float4Data))]
        [MemberData(nameof(Float3Data))]
        public void ConstructorToVector4(IPixel packedVector, float[] expectedVector4Components)
        {
            // Arrange
            int precision = 2;
            // using float array to work around a bug in xunit corruptint the state of any Vector4 passed as MemberData
            Vector4 expectedVector4 = new Vector4(expectedVector4Components[0], expectedVector4Components[1], expectedVector4Components[2], expectedVector4Components[3]);

            // Act
            Vector4 vector4 = packedVector.ToVector4();

            // Assert
            Assert.Equal(expectedVector4.X, vector4.X, precision);
            Assert.Equal(expectedVector4.Y, vector4.Y, precision);
            Assert.Equal(expectedVector4.Z, vector4.Z, precision);
            Assert.Equal(expectedVector4.W, vector4.W, precision);
        }
    }
}
