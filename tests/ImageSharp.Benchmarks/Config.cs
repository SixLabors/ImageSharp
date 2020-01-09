// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;

namespace SixLabors.ImageSharp.Benchmarks
{
    public class Config : ManualConfig
    {
        public Config()
        {
            this.Add(MemoryDiagnoser.Default);
        }

        public class ShortClr : Config
        {
            public ShortClr()
            {
                this.Add(
                    Job.Default.With(ClrRuntime.Net472).WithLaunchCount(1).WithWarmupCount(3).WithIterationCount(3),
                    Job.Default.With(CoreRuntime.Core21).WithLaunchCount(1).WithWarmupCount(3).WithIterationCount(3)
                );
            }
        }
    }
}
