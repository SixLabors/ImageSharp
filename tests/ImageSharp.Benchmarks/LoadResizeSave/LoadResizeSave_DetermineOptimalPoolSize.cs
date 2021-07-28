// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Benchmarks.LoadResizeSave
{
    [MemoryDiagnoser]
    [ShortRunJob]
    public class LoadResizeSave_DetermineOptimalPoolSize
    {
        public const int OneMegaByte = 1024 * 1024;
        private LoadResizeSaveStressRunner runner;

        [Params(-1, 8)]
        public int Parallelism { get; set; }

        [Params(128, 256, 512, 1024, 2048)]
        public long PoolSize { get; set; }

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
                new DefaultMemoryAllocator(2 * OneMegaByte, this.PoolSize * OneMegaByte, 32 * OneMegaByte);
            this.runner.MaxDegreeOfParallelism = this.Parallelism;
        }

        [Benchmark]
        public void LoadResizeSave() => this.runner.ForEachImageParallel(this.runner.ImageSharpResize);

        // Results 2021 July (Intel Core i9-10900X CPU 3.70GHz, 1 CPU, 20 logical and 10 physical cores)
        //
        //
        // |         Method | Parallelism | PoolSize |       Mean |      Error |    StdDev |     Gen 0 |     Gen 1 |     Gen 2 | Allocated |
        // |--------------- |------------ |--------- |-----------:|-----------:|----------:|----------:|----------:|----------:|----------:|
        // | LoadResizeSave |          -1 |      128 |   700.5 ms |   300.1 ms |  16.45 ms | 6000.0000 | 1000.0000 |         - |    110 MB |
        // | LoadResizeSave |          -1 |      256 |   772.0 ms | 3,177.6 ms | 174.17 ms | 6000.0000 | 2000.0000 | 1000.0000 |    250 MB |
        // | LoadResizeSave |          -1 |      512 |   668.0 ms |   110.4 ms |   6.05 ms | 5000.0000 | 3000.0000 | 1000.0000 |    494 MB |
        // | LoadResizeSave |          -1 |     1024 |   665.0 ms |   672.9 ms |  36.89 ms | 2000.0000 | 1000.0000 | 1000.0000 |  1,026 MB |
        // | LoadResizeSave |          -1 |     2048 |   601.9 ms |   203.3 ms |  11.14 ms | 1000.0000 | 1000.0000 | 1000.0000 |  1,418 MB |
        // | LoadResizeSave |           8 |      128 | 1,148.3 ms |   955.8 ms |  52.39 ms | 9000.0000 | 2000.0000 | 1000.0000 |    131 MB |
        // | LoadResizeSave |           8 |      256 | 1,155.5 ms |   652.0 ms |  35.74 ms | 5000.0000 | 2000.0000 | 1000.0000 |    258 MB |
        // | LoadResizeSave |           8 |      512 | 1,114.4 ms |   363.9 ms |  19.94 ms | 3000.0000 | 1000.0000 | 1000.0000 |    506 MB |
        // | LoadResizeSave |           8 |     1024 | 1,105.2 ms | 1,003.9 ms |  55.03 ms | 1000.0000 | 1000.0000 | 1000.0000 |    732 MB |
        // | LoadResizeSave |           8 |     2048 | 1,085.4 ms |   114.0 ms |   6.25 ms | 1000.0000 | 1000.0000 | 1000.0000 |    738 MB |
    }
}
