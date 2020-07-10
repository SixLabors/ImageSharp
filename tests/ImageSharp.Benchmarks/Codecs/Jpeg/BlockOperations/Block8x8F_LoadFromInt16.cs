// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;

using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.Formats.Jpeg.Components;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg.BlockOperations
{
    public class Block8x8F_LoadFromInt16
    {
        private Block8x8 source;

        private Block8x8F dest = default;

        [GlobalSetup]
        public void Setup()
        {
            if (Vector<float>.Count != 8)
            {
                throw new NotSupportedException("Vector<float>.Count != 8");
            }

            for (short i = 0; i < Block8x8F.Size; i++)
            {
                this.source[i] = i;
            }
        }

        [Benchmark(Baseline = true)]
        public void Scalar()
        {
            this.dest.LoadFromInt16Scalar(ref this.source);
        }

        [Benchmark]
        public void ExtendedAvx2()
        {
            this.dest.LoadFromInt16ExtendedAvx2(ref this.source);
        }

        // RESULT:
        //        Method |     Mean |     Error |    StdDev | Scaled |
        // ------------- |---------:|----------:|----------:|-------:|
        //        Scalar | 34.88 ns | 0.3296 ns | 0.3083 ns |   1.00 |
        //  ExtendedAvx2 | 21.58 ns | 0.2125 ns | 0.1884 ns |   0.62 |
    }
}
