// <copyright file="Program.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace SixLabors.ImageSharp.Sandbox46
{
    using System;
    using System.Runtime.DesignerServices;

    using SixLabors.ImageSharp.Tests;
    using SixLabors.ImageSharp.Tests.Colors;
    using SixLabors.ImageSharp.Tests.Formats.Jpg;
    using SixLabors.ImageSharp.Tests.PixelFormats;
    using SixLabors.ImageSharp.Tests.Processing.Processors.Transforms;
    using SixLabors.ImageSharp.Tests.Processing.Transforms;

    using Xunit.Abstractions;

    public class Program
    {
        private class ConsoleOutput : ITestOutputHelper
        {
            public void WriteLine(string message)
            {
                Console.WriteLine(message);
            }

            public void WriteLine(string format, params object[] args)
            {
                Console.WriteLine(format, args);
            }
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
            RunJpegColorProfilingTests();

            // RunDecodeJpegProfilingTests();
            // RunToVector4ProfilingTest();
            // RunResizeProfilingTest();

            Console.ReadLine();
        }

        private static void RunJpegColorProfilingTests()
        {
            new JpegColorConverterTests(new ConsoleOutput()).BenchmarkYCbCr(false);
            new JpegColorConverterTests(new ConsoleOutput()).BenchmarkYCbCr(true);
        }

        private static void RunResizeProfilingTest()
        {
            ResizeProfilingBenchmarks test = new ResizeProfilingBenchmarks(new ConsoleOutput());
            test.ResizeBicubic(2000, 2000);
        }

        private static void RunToVector4ProfilingTest()
        {
            PixelOperationsTests.Rgba32 tests = new PixelOperationsTests.Rgba32(new ConsoleOutput());
            tests.Benchmark_ToVector4();
        }

        private static void RunDecodeJpegProfilingTests()
        {
            Console.WriteLine("RunDecodeJpegProfilingTests...");
            JpegProfilingBenchmarks benchmarks = new JpegProfilingBenchmarks(new ConsoleOutput());
            foreach (object[] data in JpegProfilingBenchmarks.DecodeJpegData)
            {
                string fileName = (string)data[0];
                benchmarks.DecodeJpeg(fileName);
            }
        }
    }
}
