// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;

using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Icc
{
    using SixLabors.ImageSharp;

    public class IccDataWriterMatrixTests
    {
        [Theory]
        [MemberData(nameof(IccTestDataMatrix.Matrix2D_FloatArrayTestData), MemberType = typeof(IccTestDataMatrix))]
        public void WriteMatrix2D_Array(byte[] expected, int xCount, int yCount, bool isSingle, float[,] data)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteMatrix(data, isSingle);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataMatrix.Matrix2D_Matrix4x4TestData), MemberType = typeof(IccTestDataMatrix))]
        public void WriteMatrix2D_Matrix4x4(byte[] expected, int xCount, int yCount, bool isSingle, Matrix4x4 data)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteMatrix(data, isSingle);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataMatrix.Matrix2D_DenseMatrixTestData), MemberType = typeof(IccTestDataMatrix))]
        internal void WriteMatrix2D_DenseMatrix(byte[] expected, int xCount, int yCount, bool isSingle, in DenseMatrix<float> data)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteMatrix(data, isSingle);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataMatrix.Matrix1D_ArrayTestData), MemberType = typeof(IccTestDataMatrix))]
        public void WriteMatrix1D_Array(byte[] expected, int yCount, bool isSingle, float[] data)
        {
            IccDataWriter writer = this.CreateWriter();

            writer.WriteMatrix(data, isSingle);
            byte[] output = writer.GetData();

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataMatrix.Matrix1D_Vector3TestData), MemberType = typeof(IccTestDataMatrix))]
        public void WriteMatrix1D_Vector3(byte[] expected, int yCount, bool isSingle, Vector3 data)
        {
            IccDataWriter writer = this.CreateWriter();

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
