// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Primitives;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Primitives
{
    public class DenseMatrixTests
    {
        private static readonly float[,] FloydSteinbergMatrix =
        {
            { 0, 0, 7 },
            { 3, 5, 1 }
        };

        [Fact]
        public void DenseMatrixThrowsOnNullInitializer()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                var dense = new DenseMatrix<float>(null);
            });
        }

        [Fact]
        public void DenseMatrixThrowsOnEmptyZeroWidth()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var dense = new DenseMatrix<float>(0, 10);
            });
        }

        [Fact]
        public void DenseMatrixThrowsOnEmptyZeroHeight()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var dense = new DenseMatrix<float>(10, 0);
            });
        }

        [Fact]
        public void DenseMatrixThrowsOnEmptyInitializer()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                var dense = new DenseMatrix<float>(new float[0, 0]);
            });
        }

        [Fact]
        public void DenseMatrixReturnsCorrectDimensions()
        {
            var dense = new DenseMatrix<float>(FloydSteinbergMatrix);
            Assert.True(dense.Columns == FloydSteinbergMatrix.GetLength(1));
            Assert.True(dense.Rows == FloydSteinbergMatrix.GetLength(0));
            Assert.Equal(3, dense.Columns);
            Assert.Equal(2, dense.Rows);
        }

        [Fact]
        public void DenseMatrixGetReturnsCorrectResults()
        {
            DenseMatrix<float> dense = FloydSteinbergMatrix;

            for (int row = 0; row < dense.Rows; row++)
            {
                for (int column = 0; column < dense.Columns; column++)
                {
                    Assert.True(Math.Abs(dense[row, column] - FloydSteinbergMatrix[row, column]) < Constants.Epsilon);
                }
            }
        }

        [Fact]
        public void DenseMatrixGetSetReturnsCorrectResults()
        {
            var dense = new DenseMatrix<int>(4, 4);
            const int Val = 5;

            dense[3, 3] = Val;

            Assert.Equal(Val, dense[3, 3]);
        }

        [Fact]
        public void DenseMatrixCanFillAndClear()
        {
            var dense = new DenseMatrix<int>(9);
            dense.Fill(4);

            for (int i = 0; i < dense.Data.Length; i++)
            {
                Assert.Equal(4, dense.Data[i]);
            }

            dense.Clear();

            for (int i = 0; i < dense.Data.Length; i++)
            {
                Assert.Equal(0, dense.Data[i]);
            }
        }
    }
}