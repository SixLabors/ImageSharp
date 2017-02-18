// <copyright file="Array2D.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Benchmarks.General
{
    using System;

    using BenchmarkDotNet.Attributes;

    public class Array2D
    {
        private float[] flatArray;

        private float[,] array2D;

        private float[][] jaggedData;

        private Fast2DArray<float> fastData;
        
        [Params(4, 16, 128)]
        public int Count { get; set; }

        public int Min { get; private set; }
        public int Max { get; private set; }

        [Setup]
        public void SetUp()
        {
            this.flatArray = new float[this.Count * this.Count];
            this.array2D = new float[this.Count, this.Count];
            this.jaggedData = new float[this.Count][];

            for (int i = 0; i < this.Count; i++)
            {
                this.jaggedData[i] = new float[this.Count];
            }

            this.fastData = new Fast2DArray<float>(this.array2D);

            this.Min = this.Count / 2 - 10;
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
                    s += a[count * i + j];
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
                    s += a[i, j];
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
                    s += a[i][j];
                }
            }
            return s;
        }

        [Benchmark(Description = "Array access using Fast2DArray")]
        public float ArrayFastIndex()
        {
            float s = 0;
            Fast2DArray<float> a = this.fastData;
            for (int i = this.Min; i < this.Max; i++)
            {
                for (int j = this.Min; j < this.Max; j++)
                {
                    s += a[i, j];
                }
            }
            return s;
        }
    }
}
