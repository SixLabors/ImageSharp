// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using SixLabors.ImageSharp.Metadata.Profiles.Icc;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Icc
{
    public class IccDataReaderMatrixTests
    {
        [Theory]
        [MemberData(nameof(IccTestDataMatrix.Matrix2D_FloatArrayTestData), MemberType = typeof(IccTestDataMatrix))]
        public void ReadMatrix2D(byte[] data, int xCount, int yCount, bool isSingle, float[,] expected)
        {
            IccDataReader reader = this.CreateReader(data);

            float[,] output = reader.ReadMatrix(xCount, yCount, isSingle);

            Assert.Equal(expected, output);
        }

        [Theory]
        [MemberData(nameof(IccTestDataMatrix.Matrix1D_ArrayTestData), MemberType = typeof(IccTestDataMatrix))]
        public void ReadMatrix1D(byte[] data, int yCount, bool isSingle, float[] expected)
        {
            IccDataReader reader = this.CreateReader(data);

            float[] output = reader.ReadMatrix(yCount, isSingle);

            Assert.Equal(expected, output);
        }

        private IccDataReader CreateReader(byte[] data)
        {
            return new IccDataReader(data);
        }
    }
}
