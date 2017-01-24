// <copyright file="Config.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using BenchmarkDotNet.Configs;

namespace ImageSharp.Benchmarks
{
    using BenchmarkDotNet.Jobs;

    public class Config : ManualConfig
    {
        public Config()
        {
            // Uncomment if you want to use any of the diagnoser
            this.Add(new BenchmarkDotNet.Diagnosers.MemoryDiagnoser());
        }

        public class Short : Config
        {
            public Short()
            {
                this.Add(
                    Job.Default.WithLaunchCount(1)
                        .WithWarmupCount(3)
                        .WithTargetCount(3)
                        );
            }
        }
    }
}