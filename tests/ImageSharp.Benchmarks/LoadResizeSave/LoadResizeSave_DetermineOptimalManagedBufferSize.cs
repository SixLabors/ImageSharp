// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Benchmarks.LoadResizeSave
{
    [MemoryDiagnoser]
    [ShortRunJob]
    public class LoadResizeSave_DetermineOptimalManagedBufferSize
    {
        public const int OneMegaByte = 1024 * 1024;
        private LoadResizeSaveStressRunner runner;

        [Params(2, 4, 8, 16, 32, 64)]
        public int BufferSize { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            this.runner = new LoadResizeSaveStressRunner()
            {
                ImageCount = Environment.ProcessorCount,
            };
            this.runner.Init();
        }

        [IterationSetup]
        public void IterationSetup()
        {
            Configuration.Default.MemoryAllocator.ReleaseRetainedResources();
            GC.Collect();
            Configuration.Default.MemoryAllocator =
                new DefaultMemoryAllocator(this.BufferSize * OneMegaByte, 4096L * OneMegaByte, 32 * OneMegaByte);
        }

        [Benchmark]
        public void LoadResizeSave() => this.runner.ForEachImageParallel(this.runner.ImageSharpResize);

        // Results 2021 July (Intel Core i9-10900X CPU 3.70GHz, 1 CPU, 20 logical and 10 physical cores)
        // |         Method | BufferSize |     Mean |    Error |   StdDev |     Gen 0 |     Gen 1 |     Gen 2 | Allocated |
        // |--------------- |----------- |---------:|---------:|---------:|----------:|----------:|----------:|----------:|
        // | LoadResizeSave |          2 | 612.4 ms | 422.8 ms | 23.17 ms | 1000.0000 | 1000.0000 | 1000.0000 |      1 GB |
        // | LoadResizeSave |          4 | 605.8 ms | 117.8 ms |  6.46 ms | 1000.0000 | 1000.0000 | 1000.0000 |      1 GB |
        // | LoadResizeSave |          8 | 623.1 ms | 243.6 ms | 13.35 ms | 1000.0000 | 1000.0000 | 1000.0000 |      1 GB |
        // | LoadResizeSave |         16 | 636.4 ms | 468.4 ms | 25.67 ms | 1000.0000 | 1000.0000 | 1000.0000 |      2 GB |
        // | LoadResizeSave |         32 | 631.0 ms | 270.6 ms | 14.83 ms | 1000.0000 | 1000.0000 | 1000.0000 |      2 GB |
        // | LoadResizeSave |         64 | 642.4 ms | 156.8 ms |  8.60 ms | 2000.0000 | 2000.0000 | 2000.0000 |      2 GB |
    }
}
