// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

#if OS_WINDOWS
using System.Security.Principal;
using BenchmarkDotNet.Diagnostics.Windows;
#endif
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace SixLabors.ImageSharp.Benchmarks;

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

        this.SummaryStyle = SummaryStyle.Default.WithMaxParameterColumnWidth(50);
    }

    public class Standard : Config
    {
        public Standard() => this.AddJob(
                Job.Default.WithRuntime(CoreRuntime.Core80).WithArguments([new MsBuildArgument("/p:DebugType=portable")]));
    }

    public class Short : Config
    {
        public Short() => this.AddJob(
                Job.Default.WithRuntime(CoreRuntime.Core80)
                           .WithLaunchCount(1)
                           .WithWarmupCount(3)
                           .WithIterationCount(3)
                           .WithArguments([new MsBuildArgument("/p:DebugType=portable")]));
    }

    public class StandardInProcess : Config
    {
        public StandardInProcess() => this.AddJob(
            Job.Default
                .WithRuntime(CoreRuntime.Core80)
                .WithToolchain(InProcessEmitToolchain.Instance)
                .WithArguments([new MsBuildArgument("/p:DebugType=portable")]));
    }

#if OS_WINDOWS
    private bool IsElevated => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
#endif
}
