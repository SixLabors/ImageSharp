// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Reflection;
using SixLabors.ImageSharp.Tests.PixelFormats.PixelOperations;
using SixLabors.ImageSharp.Tests.ProfilingBenchmarks;
using Xunit.Abstractions;

// in this file, comments are used for disabling stuff for local execution
#pragma warning disable SA1515
#pragma warning disable SA1512

namespace SixLabors.ImageSharp.Tests.ProfilingSandbox;

public class Program
{
    private class ConsoleOutput : ITestOutputHelper
    {
        public void WriteLine(string message) => Console.WriteLine(message);

        public void WriteLine(string format, params object[] args) => Console.WriteLine(format, args);
    }

    /// <summary>
    /// The main entry point. Useful for executing benchmarks and performance unit tests manually,
    /// when the IDE test runners lack some of the functionality. Eg.: it's not possible to run JetBrains memory profiler for unit tests.
    /// </summary>
    /// <param name="args">
    /// The arguments to pass to the program.
    /// </param>
    public static void Main(string[] args)
    {
        try
        {
            LoadResizeSaveParallelMemoryStress.Run(args);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }

        // RunJpegEncoderProfilingTests();
        // RunJpegColorProfilingTests();
        // RunDecodeJpegProfilingTests();
        // RunToVector4ProfilingTest();
        // RunResizeProfilingTest();

        // Console.ReadLine();
    }

    private static Version GetNetCoreVersion()
    {
        Assembly assembly = typeof(System.Runtime.GCSettings).GetTypeInfo().Assembly;
        Console.WriteLine(assembly.Location);
        string[] assemblyPath = assembly.Location.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        int netCoreAppIndex = Array.IndexOf(assemblyPath, "Microsoft.NETCore.App");
        if (netCoreAppIndex > 0 && netCoreAppIndex < assemblyPath.Length - 2)
        {
            return Version.Parse(assemblyPath[netCoreAppIndex + 1]);
        }

        return null;
    }

    private static void RunResizeProfilingTest()
    {
        ResizeProfilingBenchmarks test = new(new ConsoleOutput());
        test.ResizeBicubic(4000, 4000);
    }

    private static void RunToVector4ProfilingTest()
    {
        PixelOperationsTests.Rgba32_OperationsTests tests = new(new ConsoleOutput());
        tests.Benchmark_ToVector4();
    }
}
