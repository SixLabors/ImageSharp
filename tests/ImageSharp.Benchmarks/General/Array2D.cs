// <copyright file="Array2D.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace SixLabors.ImageSharp.Benchmarks.General
{
    using System;

    using BenchmarkDotNet.Attributes;

    using SixLabors.ImageSharp.Primitives;

    /**
     *                                Method | Count |     Mean |    Error |   StdDev | Scaled | ScaledSD |
-------------------------------------------- |------ |---------:|---------:|---------:|-------:|---------:|
 'Emulated 2D array access using flat array' |    32 | 224.2 ns | 4.739 ns | 13.75 ns |   0.65 |     0.07 |
               'Array access using 2D array' |    32 | 346.6 ns | 9.225 ns | 26.91 ns |   1.00 |     0.00 |
         'Array access using a jagged array' |    32 | 229.3 ns | 6.028 ns | 17.58 ns |   0.67 |     0.07 |
            'Array access using DenseMatrix' |    32 | 223.2 ns | 5.248 ns | 15.22 ns |   0.65 |     0.07 |

     *
     */

    public class Array2D
    {
        private float[] flatArray;

        private float[,] array2D;

        private float[][] jaggedData;

        private DenseMatrix<float> matrix;

        [Params(4, 16, 32)]
        public int Count { get; set; }

        public int Min { get; private set; }
        public int Max { get; private set; }

        [GlobalSetup]
        public void SetUp()
        {
            this.flatArray = new float[this.Count * this.Count];
            this.array2D = new float[this.Count, this.Count];
            this.jaggedData = new float[this.Count][];

            for (int i = 0; i < this.Count; i++)
            {
                this.jaggedData[i] = new float[this.Count];
            }

            this.matrix = new DenseMatrix<float>(this.array2D);

            this.Min = (this.Count / 2) - 10;
            this.Min = Math.Max(0, this.Min);
            this.Max = this.Min + Math.Min(10, this.Count);
        }

        [Benchmark(Description = "Emulated 2D array access using flat array")]
        public float FlatArrayIndex()
        {
            float[] a = this.flatArray;
            float s = 0;
            int count = this.Count;
            for (int i = this.Min; i < this.Max; i++)
            {
                for (int j = this.Min; j < this.Max; j++)
                {
                    ref float v = ref a[count * i + j];
                    v = i * j;
                    s += v;
                }
            }
            return s;
        }

        [Benchmark(Baseline = true, Description = "Array access using 2D array")]
        public float Array2DIndex()
        {
            float s = 0;
            float[,] a = this.array2D;
            for (int i = this.Min; i < this.Max; i++)
            {
                for (int j = this.Min; j < this.Max; j++)
                {
                    ref float v = ref a[i, j];
                    v = i * j;
                    s += v;
                }
            }
            return s;
        }

        [Benchmark(Description = "Array access using a jagged array")]
        public float ArrayJaggedIndex()
        {
            float s = 0;
            float[][] a = this.jaggedData;
            for (int i = this.Min; i < this.Max; i++)
            {
                for (int j = this.Min; j < this.Max; j++)
                {
                    ref float v = ref a[i][j];
                    v = i * j;
                    s += v;
                }
            }
            return s;
        }

        [Benchmark(Description = "Array access using DenseMatrix")]
        public float ArrayMatrixIndex()
        {
            float s = 0;
            DenseMatrix<float> a = this.matrix;
            for (int i = this.Min; i < this.Max; i++)
            {
                for (int j = this.Min; j < this.Max; j++)
                {
                    ref float v = ref a[i, j];
                    v = i * j;
                    s += v;
                }
            }
            return s;
        }
    }
}