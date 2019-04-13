// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Configs;

using BenchmarkDotNet.Jobs;

namespace SixLabors.ImageSharp.Benchmarks
{
    public class Config : ManualConfig
    {
        public Config()
        {
            // Uncomment if you want to use any of the diagnoser
            this.Add(new BenchmarkDotNet.Diagnosers.MemoryDiagnoser());
        }

        public class ShortClr : Config
        {
            public ShortClr()
            {
                this.Add(
                    Job.Clr.WithLaunchCount(1).WithWarmupCount(3).WithIterationCount(3),
                    Job.Core.WithLaunchCount(1).WithWarmupCount(3).WithIterationCount(3)
                );
            }
        }
    }
}