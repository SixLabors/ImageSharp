// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading;
using SixLabors.ImageSharp.Memory.Internals;
using SixLabors.ImageSharp.Tests.Formats.Jpg;
using SixLabors.ImageSharp.Tests.PixelFormats.PixelOperations;
using SixLabors.ImageSharp.Tests.ProfilingBenchmarks;
using Xunit.Abstractions;

// in this file, comments are used for disabling stuff for local execution
#pragma warning disable SA1515
#pragma warning disable SA1512

namespace SixLabors.ImageSharp.Tests.ProfilingSandbox
{
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
            LoadResizeSaveParallelMemoryStress.Run(args);
            TrimPoolTest();
            // RunJpegEncoderProfilingTests();
            // RunJpegColorProfilingTests();
            // RunDecodeJpegProfilingTests();
            // RunToVector4ProfilingTest();
            // RunResizeProfilingTest();

            // Console.ReadLine();
        }

        private static void TrimPoolTest()
        {
            var pool = new UniformByteArrayPool(1024 * 1024, 128);
            Console.WriteLine($"Before use: {CurrentM()} MB");
            UsePool(pool);
            Console.WriteLine($"After use: {CurrentM()} MB");

            GC.Collect();
            WaitOneTrim(); // 48
            Console.WriteLine($"Trim 1: {CurrentM()} MB");
            WaitOneTrim(); // 36
            Console.WriteLine($"Trim 2:{CurrentM()} MB");
            WaitOneTrim(); // 27
            Console.WriteLine($"Trim 3:{CurrentM()} MB");
            WaitOneTrim(); // 20
            Console.WriteLine($"Trim 4:{CurrentM()} MB");

            static void WaitOneTrim()
            {
                GC.WaitForPendingFinalizers();
                Thread.Sleep(20); // Wait for the trimming work item to complete on ThreadPool
                GC.Collect();
            }

            static void UsePool(UniformByteArrayPool pool)
            {
                // 16 16 16 16 | 16 16 16 16
                byte[][] a1 = pool.Rent(16);
                byte[][] a2 = pool.Rent(32);
                pool.Return(a1);
                byte[][] a3 = pool.Rent(16);
                pool.Return(a2);
                byte[][] a4 = pool.Rent(32);
                byte[][] a5 = pool.Rent(16);
                pool.Return(a4);
                pool.Return(a3);
                pool.Return(a5);

                // Pool should retain 64MB at this point
            }

            static double CurrentM() => GC.GetTotalMemory(false) / 1048576.0;
        }

        private static void RunJpegEncoderProfilingTests()
        {
            var benchmarks = new JpegProfilingBenchmarks(new ConsoleOutput());
            benchmarks.EncodeJpeg_SingleMidSize();
        }

        private static void RunJpegColorProfilingTests()
        {
            new JpegColorConverterTests(new ConsoleOutput()).BenchmarkYCbCr(false);
            new JpegColorConverterTests(new ConsoleOutput()).BenchmarkYCbCr(true);
        }

        private static void RunResizeProfilingTest()
        {
            var test = new ResizeProfilingBenchmarks(new ConsoleOutput());
            test.ResizeBicubic(4000, 4000);
        }

        private static void RunToVector4ProfilingTest()
        {
            var tests = new PixelOperationsTests.Rgba32_OperationsTests(new ConsoleOutput());
            tests.Benchmark_ToVector4();
        }

        private static void RunDecodeJpegProfilingTests()
        {
            Console.WriteLine("RunDecodeJpegProfilingTests...");
            var benchmarks = new JpegProfilingBenchmarks(new ConsoleOutput());
            foreach (object[] data in JpegProfilingBenchmarks.DecodeJpegData)
            {
                string fileName = (string)data[0];
                int executionCount = (int)data[1];
                benchmarks.DecodeJpeg(fileName, executionCount);
            }

            Console.WriteLine("DONE.");
        }
    }
}
