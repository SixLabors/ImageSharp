// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using SixLabors.ImageSharp.Metadata.Profiles.Icc;

namespace SixLabors.ImageSharp.Tests.Colorspaces.Icc
{
    public class IccConversionDataMatrix
    {
        public static float[,] Matrix3x3Random = { { 0.1f, 0.2f, 0.3f }, { 0.4f, 0.5f, 0.6f }, { 0.7f, 0.8f, 0.9f } };
        public static float[,] Matrix3x3Identity = { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
        
        public static object[][] MatrixConversionTestData =
        {
            new object[] { CreateMatrix(Matrix3x3Identity), Vector3.Zero, new Vector4(0.5f, 0.5f, 0.5f, 0), new Vector4(0.5f, 0.5f, 0.5f, 0) },
            new object[] { CreateMatrix(Matrix3x3Identity), new Vector3(0.2f, 0.2f, 0.2f), new Vector4(0.5f, 0.5f, 0.5f, 0), new Vector4(0.7f, 0.7f, 0.7f, 0) },
            new object[] { CreateMatrix(Matrix3x3Random), Vector3.Zero, new Vector4(0.5f, 0.5f, 0.5f, 0), new Vector4(0.6f, 0.75f, 0.9f, 0) },
            new object[] { CreateMatrix(Matrix3x3Random), new Vector3(0.1f, 0.2f, 0.3f), new Vector4(0.5f, 0.5f, 0.5f, 0), new Vector4(0.7f, 0.95f, 1.2f, 0) },
            new object[] { CreateMatrix(Matrix3x3Random), Vector3.Zero, new Vector4(0.2f, 0.4f, 0.7f, 0), new Vector4(0.67f, 0.8f, 0.93f, 0) },
            new object[] { CreateMatrix(Matrix3x3Random), new Vector3(0.1f, 0.2f, 0.3f), new Vector4(0.2f, 0.4f, 0.7f, 0), new Vector4(0.77f, 1, 1.23f, 0) },
        };
        
        private static Matrix4x4 CreateMatrix(float[,] matrix)
        {
            return new Matrix4x4(
                matrix[0, 0],
                matrix[0, 1],
                matrix[0, 2],
                0,
                matrix[1, 0],
                matrix[1, 1],
                matrix[1, 2],
                0,
                matrix[2, 0],
                matrix[2, 1],
                matrix[2, 2],
                0,
                0,
                0,
                0,
                1);
        }
    }
}
