// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Tests.PixelFormats.PixelOperations;
using SixLabors.ImageSharp.Tests.ProfilingBenchmarks;
using SixLabors.ImageSharp.Tests.ProfilingSandbox;
using Xunit.Abstractions;

// in this file, comments are used for disabling stuff for local execution
#pragma warning disable SA1515
#pragma warning disable SA1512

// LoadResizeSaveParallelMemoryStress.Run(args);
// ParallelProcessingStress.RunExperiment(args);
// ParallelProcessingStress.Run(args);
await ProcessorThroughputBenchmark.RunAsync(args);

// RunToVector4ProfilingTest();
// RunResizeProfilingTest();

static void RunResizeProfilingTest()
{
    ResizeProfilingBenchmarks test = new(new ConsoleOutput());
    test.ResizeBicubic(4000, 4000);
}

static void RunToVector4ProfilingTest()
{
    PixelOperationsTests.Rgba32_OperationsTests tests = new(new ConsoleOutput());
    tests.Benchmark_ToVector4();
}

sealed class ConsoleOutput : ITestOutputHelper
{
    public void WriteLine(string message) => Console.WriteLine(message);

    public void WriteLine(string format, params object[] args) => Console.WriteLine(format, args);
}
