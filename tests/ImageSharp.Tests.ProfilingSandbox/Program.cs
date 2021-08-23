// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.IO;
using SixLabors.ImageSharp.Formats.Jpeg;
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
            /* Master */
            // Elapsed: 5431ms across 200 iterations
            // Average: 27,155ms

            /* Inserting stuff bytes later */
            // Elapsed: 5300ms across 200 iterations
            // Average: 26,5ms

            /* Flush if check */
            // Elapsed: 5209ms across 200 iterations
            // Average: 26,045ms

            /* [INVALID] int32 flush - invalid flush order */
            // Elapsed: 4784ms across 200 iterations
            // Average: 23,92ms

            /* int32 flush - correct flush order */
            // Elapsed: 5049ms across 200 iterations
            // Average: 25,245ms

            /* int32 flush - identical file output */
            // Elapsed: 4800ms across 200 iterations
            // Average: 24.00ms

            /* int32 flush - optimized huffman storage & reduced instructions per Emit() */
            // Elapsed: 4680ms across 200 iterations
            // Average: 23,4ms

            /* int32 flush - merged prefix & value Emit() call */
            // Elapsed: 4644ms across 200 iterations
            // Average: 23,22ms

            BenchmarkEncoder("uniform_size", 200, 100);

            //ReEncodeImage("uniform_size", 100);

            Console.WriteLine("Done.");
        }

        const string pathTemplate = "C:\\Users\\pl4nu\\Downloads\\{0}.jpg";

        private static void BenchmarkEncoder(string fileName, int iterations, int quality)
        {
            string loadPath = String.Format(pathTemplate, fileName);

            using var saveStream = new MemoryStream();

            var decoder = new JpegDecoder { IgnoreMetadata = true };
            using Image img = decoder.Decode(Configuration.Default, new FileStream(loadPath, FileMode.Open));

            var encoder = new JpegEncoder()
            {
                Quality = quality,
                ColorType = JpegColorType.YCbCr,
                Subsample = JpegSubsample.Ratio444
            };

            Stopwatch sw = new Stopwatch();
            sw.Start();
            for (int i = 0; i < iterations; i++)
            {
                img.SaveAsJpeg(saveStream, encoder);
                saveStream.Position = 0;
            }
            sw.Stop();

            Console.WriteLine($"// Elapsed: {sw.ElapsedMilliseconds}ms across {iterations} iterations\n// Average: {(double)sw.ElapsedMilliseconds / iterations}ms");
        }

        private static void ReEncodeImage(string fileName, int quality)
        {
            string loadPath = String.Format(pathTemplate, fileName);
            using Image img = Image.Load(loadPath);

            string savePath = String.Format(pathTemplate, $"testSave_{fileName}");
            var encoder = new JpegEncoder()
            {
                Quality = quality,
                ColorType = JpegColorType.YCbCr,
                Subsample = JpegSubsample.Ratio444
            };
            img.SaveAsJpeg(savePath, encoder);
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
