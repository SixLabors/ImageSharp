// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.IO;
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
            // RunJpegEncoderProfilingTests();
            // RunJpegColorProfilingTests();
            // RunDecodeJpegProfilingTests();
            // RunToVector4ProfilingTest();
            // RunResizeProfilingTest();

            //Test_Performance(20);

            //Test_DebugRun("chroma_444_16x16", true);
            //Console.WriteLine();
            //Test_DebugRun("chroma_420_16x16", true);
            //Console.WriteLine();
            //Test_DebugRun("444_14x14");
            //Console.WriteLine();
            //Test_DebugRun("baseline_4k_444", false);
            //Console.WriteLine();
            //Test_DebugRun("progressive_4k_444", true);
            //Console.WriteLine();
            //Test_DebugRun("baseline_4k_420", false);
            //Console.WriteLine();
            //Test_DebugRun("cmyk_jpeg");
            //Console.WriteLine();
            //Test_DebugRun("Channel_digital_image_CMYK_color");
            //Console.WriteLine();

            //Test_DebugRun("test_baseline_4k_444", false);
            //Console.WriteLine();
            //Test_DebugRun("test_progressive_4k_444", false);
            //Console.WriteLine();
            //Test_DebugRun("test_baseline_4k_420", false);
            //Console.WriteLine();

            // Binary size of this must be ~2096kb
            //Test_DebugRun("422", true);

            //Test_DebugRun("baseline_4k_420", false);
            //Test_DebugRun("baseline_s444_q100", false);
            //Test_DebugRun("progressive_s420_q100", false);
            //Test_DebugRun("baseline_4k_420", true);
            Test_DebugRun("baseline_s444_q100", true);
            //Test_DebugRun("progressive_s420_q100", true);

            //Console.ReadLine();
        }

        public static void Test_Performance(int iterations)
        {
            using var stream = new MemoryStream(File.ReadAllBytes("C:\\Users\\pl4nu\\Downloads\\progressive_4k_444.jpg"));
            //using var stream = new MemoryStream(File.ReadAllBytes("C:\\Users\\pl4nu\\Downloads\\baseline_4k_444.jpg"));
            var sw = new Stopwatch();
            sw.Start();
            for (int i = 0; i < iterations; i++)
            {
                using var img = Image.Load(stream);
                stream.Position = 0;
            }

            sw.Stop();
            Console.WriteLine($"Elapsed: {sw.ElapsedMilliseconds}ms\nPer invocation: {sw.ElapsedMilliseconds / iterations}ms");
        }

        public static void Test_DebugRun(string name, bool save = false)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"img: {name}");
            Console.ResetColor();
            using var img = Image.Load($"C:\\Users\\pl4nu\\Downloads\\{name}.jpg");

            if (save)
            {
                img.SaveAsJpeg($"C:\\Users\\pl4nu\\Downloads\\test_{name}.jpg",
                    new ImageSharp.Formats.Jpeg.JpegEncoder { Subsample = ImageSharp.Formats.Jpeg.JpegSubsample.Ratio444, Quality = 100 });
            }
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
