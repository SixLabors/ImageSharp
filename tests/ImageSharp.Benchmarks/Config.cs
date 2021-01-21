// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

#if OS_WINDOWS
using System.Security.Principal;
using BenchmarkDotNet.Diagnostics.Windows;
#endif
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;

namespace SixLabors.ImageSharp.Benchmarks
{
    public partial class Config : ManualConfig
    {
        public Config()
        {
            this.AddDiagnoser(MemoryDiagnoser.Default);

#if OS_WINDOWS
            if (this.IsElevated)
            {
                this.AddDiagnoser(new NativeMemoryProfiler());
            }
#endif

        }

        public class MultiFramework : Config
        {
            public MultiFramework() => this.AddJob(
                    Job.Default.WithRuntime(ClrRuntime.Net472),
                    Job.Default.WithRuntime(CoreRuntime.Core21),
                    Job.Default.WithRuntime(CoreRuntime.Core31));
        }

        public class ShortMultiFramework : Config
        {
            public ShortMultiFramework() => this.AddJob(
                    Job.Default.WithRuntime(ClrRuntime.Net472).WithLaunchCount(1).WithWarmupCount(3).WithIterationCount(3),
                    Job.Default.WithRuntime(CoreRuntime.Core21).WithLaunchCount(1).WithWarmupCount(3).WithIterationCount(3),
                    Job.Default.WithRuntime(CoreRuntime.Core31).WithLaunchCount(1).WithWarmupCount(3).WithIterationCount(3));
        }

        public class ShortCore31 : Config
        {
            public ShortCore31()
                => this.AddJob(Job.Default.WithRuntime(CoreRuntime.Core31).WithLaunchCount(1).WithWarmupCount(3).WithIterationCount(3));
        }

#if OS_WINDOWS
        private bool IsElevated => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
#endif
    }
}
