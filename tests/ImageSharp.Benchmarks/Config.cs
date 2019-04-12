﻿// <copyright file="Config.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using BenchmarkDotNet.Configs;

namespace SixLabors.ImageSharp.Benchmarks
{
    using BenchmarkDotNet.Diagnosers;
    using BenchmarkDotNet.Jobs;

    public class Config : ManualConfig
    {
        public Config()
        {
            // Uncomment if you want to use any of the diagnoser
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
    }
}