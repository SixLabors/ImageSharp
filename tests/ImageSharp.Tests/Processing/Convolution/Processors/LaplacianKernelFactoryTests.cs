// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using SixLabors.ImageSharp.Primitives;
using SixLabors.ImageSharp.Processing.Processors.Convolution;
using Xunit;

namespace SixLabors.ImageSharp.Tests.Processing.Processors.Convolution
{
    public class LaplacianKernelFactoryTests
    {
        private static readonly ApproximateFloatComparer ApproximateComparer = new ApproximateFloatComparer(0.0001F);

        private static readonly DenseMatrix<float> Expected3x3Matrix = new DenseMatrix<float>(
            new float[,]
                {
                    { -1, -1, -1 },
                    { -1,  8, -1 },
                    { -1, -1, -1 }
                });

        private static readonly DenseMatrix<float> Expected5x5Matrix = new DenseMatrix<float>(
            new float[,]
                {
                    { -1, -1, -1,-1, -1 },
                    { -1, -1, -1,-1, -1 },
                    { -1, -1, 24,-1, -1 },
                    { -1, -1, -1,-1, -1 },
                    { -1, -1, -1,-1, -1 }
                });

        [Fact]
        public void LaplacianKernelFactoryOutOfRangeThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                LaplacianKernelFactory.CreateKernel(2);
            });
        }

        [Fact]
        public void LaplacianKernelFactoryCreatesCorrect3x3Matrix()
        {
            DenseMatrix<float> actual = LaplacianKernelFactory.CreateKernel(3);
            for (int y = 0; y < actual.Rows; y++)
            {
                for (int x = 0; x < actual.Columns; x++)
                {
                    Assert.Equal(Expected3x3Matrix[y, x], actual[y, x], ApproximateComparer);
                }
            }
        }

        [Fact]
        public void LaplacianKernelFactoryCreatesCorrect5x5Matrix()
        {
            DenseMatrix<float> actual = LaplacianKernelFactory.CreateKernel(5);
            for (int y = 0; y < actual.Rows; y++)
            {
                for (int x = 0; x < actual.Columns; x++)
                {
                    Assert.Equal(Expected5x5Matrix[y, x], actual[y, x], ApproximateComparer);
                }
            }
        }
    }
}