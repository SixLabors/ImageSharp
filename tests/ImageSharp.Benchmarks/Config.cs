// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
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
                    Job.Clr.WithLaunchCount(1).WithWarmupCount(3).WithIterationCount(3),
                    Job.Core.WithLaunchCount(1).WithWarmupCount(3).WithIterationCount(3)
                );
            }
        }

        public class LongClr : Config
        {
            public LongClr()
            {
                this.Add(
                    Job.Clr.WithLaunchCount(1).WithWarmupCount(3).WithTargetCount(5),
                    Job.Core.WithLaunchCount(1).WithWarmupCount(3).WithTargetCount(5)
                        );
            }
        }
    }
}