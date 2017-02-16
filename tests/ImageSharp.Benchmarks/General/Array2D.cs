// <copyright file="Array2D.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Benchmarks.General
{
    using BenchmarkDotNet.Attributes;

    public class Array2D
    {
        private float[,] data;

        private float[][] jaggedData;

        private Fast2DArray<float> fastData;

        [Params(10, 100, 1000, 10000)]
        public int Count { get; set; }

        public int Index { get; set; }

        [Setup]
        public void SetUp()
        {
            this.data = new float[this.Count, this.Count];
            this.jaggedData = new float[this.Count][];

            for (int i = 0; i < this.Count; i++)
            {
                this.jaggedData[i] = new float[this.Count];
            }

            this.fastData = new Fast2DArray<float>(this.data);

            this.Index = this.Count / 2;
        }

        [Benchmark(Baseline = true, Description = "Array access using 2D array")]
        public float ArrayIndex()
        {
            return this.data[this.Index, this.Index];
        }

        [Benchmark(Description = "Array access using a jagged array")]
        public float ArrayJaggedIndex()
        {
            return this.jaggedData[this.Index][this.Index];
        }

        [Benchmark(Description = "Array access using Fast2DArray")]
        public float ArrayFastIndex()
        {
            return this.fastData[this.Index, this.Index];
        }
    }
}
