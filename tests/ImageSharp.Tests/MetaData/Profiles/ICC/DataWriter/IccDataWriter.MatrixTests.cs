// <copyright file="IccDataWriter.MatrixTests.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Tests.Icc
{
    using System.Numerics;

    using ImageSharp.Memory;

    using Xunit;

    public class IccDataWriterMatrixTests
    {
        [Theory]
        [MemberData(nameof(IccTestDataMatrix.Matrix2D_FloatArrayTestData), MemberType = typeof(IccTestDataMatrix))]
        public void WriteMatrix2D_Array(byte[] expected, int xCount, int yCount, bool isSingle, float[,] data)
        {
            IccDataWriter writer = CreateWriter();

            writer.WriteMatrix(data, isSingle);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataMatrix.Matrix2D_Matrix4x4TestData), MemberType = typeof(IccTestDataMatrix))]
        public void WriteMatrix2D_Matrix4x4(byte[] expected, int xCount, int yCount, bool isSingle, Matrix4x4 data)
        {
            IccDataWriter writer = CreateWriter();

            writer.WriteMatrix(data, isSingle);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataMatrix.Matrix2D_Fast2DArrayTestData), MemberType = typeof(IccTestDataMatrix))]
        internal void WriteMatrix2D_Fast2DArray(byte[] expected, int xCount, int yCount, bool isSingle, Fast2DArray<float> data)
        {
            IccDataWriter writer = CreateWriter();

            writer.WriteMatrix(data, isSingle);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataMatrix.Matrix1D_ArrayTestData), MemberType = typeof(IccTestDataMatrix))]
        public void WriteMatrix1D_Array(byte[] expected, int yCount, bool isSingle, float[] data)
        {
            IccDataWriter writer = CreateWriter();

            writer.WriteMatrix(data, isSingle);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataMatrix.Matrix1D_Vector3TestData), MemberType = typeof(IccTestDataMatrix))]
        public void WriteMatrix1D_Vector3(byte[] expected, int yCount, bool isSingle, Vector3 data)
        {
            IccDataWriter writer = CreateWriter();

            writer.WriteMatrix(data, isSingle);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        private IccDataWriter CreateWriter()
        {
            return new IccDataWriter();
        }
    }
}
