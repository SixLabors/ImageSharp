// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Collections.Generic;
using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Colors
{
    public class ColorPackingTests
    {
        public static IEnumerable<object[]> Vector4PackData
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
                    float[] vector4Components = new float[] { vector4.X, vector4.Y, vector4.Z, vector4.W };

                    yield return new object[] { default(Argb32), vector4Components };
                    yield return new object[] { default(Bgra4444), vector4Components };
                    yield return new object[] { default(Bgra5551), vector4Components };
                    yield return new object[] { default(Byte4), vector4Components };
                    yield return new object[] { default(HalfVector4), vector4Components };
                    yield return new object[] { default(NormalizedByte4), vector4Components };
                    yield return new object[] { default(NormalizedShort4), vector4Components };
                    yield return new object[] { default(Rgba1010102), vector4Components };
                    yield return new object[] { default(Rgba64), vector4Components };
                    yield return new object[] { default(Short4), vector4Components };
                }
            }
        }

        public static IEnumerable<object[]> Vector3PackData
        {
            get
            {
                Vector4[] vector4Values = new Vector4[]
                    {
                        Vector4.One,
                        new Vector4(0, 0, 0, 1),
                        new Vector4(1, 0, 0, 1),
                        new Vector4(0, 1, 0, 1),
                        new Vector4(0, 0, 1, 1),
                    };

                foreach (Vector4 vector4 in vector4Values)
                {
                    float[] vector4Components = new float[] { vector4.X, vector4.Y, vector4.Z, vector4.W };

                    yield return new object[] { default(Argb32), vector4Components };
                    yield return new object[] { new Bgr565(), vector4Components };
                }
            }
        }

        [Theory]
        [MemberData(nameof(Vector4PackData))]
        [MemberData(nameof(Vector3PackData))]
        public void FromVector4ToVector4(IPixel packedVector, float[] vector4ComponentsToPack)
        {
            // Arrange
            int precision = 2;
            Vector4 vector4ToPack = new Vector4(vector4ComponentsToPack[0], vector4ComponentsToPack[1], vector4ComponentsToPack[2], vector4ComponentsToPack[3]);
            packedVector.PackFromVector4(vector4ToPack);

            // Act
            Vector4 vector4 = packedVector.ToVector4();

            // Assert
            Assert.Equal(vector4ToPack.X, vector4.X, precision);
            Assert.Equal(vector4ToPack.Y, vector4.Y, precision);
            Assert.Equal(vector4ToPack.Z, vector4.Z, precision);
            Assert.Equal(vector4ToPack.W, vector4.W, precision);
        }
    }
}
