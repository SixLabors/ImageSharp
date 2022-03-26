// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
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

        public static void Main(string[] args)
        {
            //ReEncodeImage("Calliphora");

            // DecodeImageResize__explicit("Calliphora", new Size(101, 150));
            // DecodeImageResize__experimental("Calliphora_aligned_size", new Size(101, 150));
            //DecodeImageResize__experimental("winter420_noninterleaved", new Size(80, 120));

            // Decode-Resize-Encode w/ Mutate()
            // Elapsed: 2504ms across 250 iterations
            // Average: 10,016ms
            BenchmarkResizingLoop__explicit("Calliphora", new Size(80, 120), 250);

            // Decode-Resize-Encode w/ downscaling decoder
            // Elapsed: 1157ms across 250 iterations
            // Average: 4,628ms
            BenchmarkResizingLoop__experimental("Calliphora", new Size(80, 120), 250);

            Console.WriteLine("Done.");
        }

        const string pathTemplate = "C:\\Users\\pl4nu\\Downloads\\{0}.jpg";

        private static void BenchmarkEncoder(string fileName, int iterations, int quality, JpegColorType color)
        {
            string loadPath = String.Format(pathTemplate, fileName);

            using var inputStream = new FileStream(loadPath, FileMode.Open);
            using var saveStream = new MemoryStream();

            var decoder = new JpegDecoder { IgnoreMetadata = true };
            using Image img = decoder.Decode(Configuration.Default, inputStream, CancellationToken.None);

            var encoder = new JpegEncoder()
            {
                Quality = quality,
                ColorType = color
            };

            Stopwatch sw = new Stopwatch();
            sw.Start();
            for (int i = 0; i < iterations; i++)
            {
                img.SaveAsJpeg(saveStream, encoder);
                saveStream.Position = 0;
            }
            sw.Stop();

            Console.WriteLine($"// Encoding q={quality} | color={color}\n" +
                $"// Elapsed: {sw.ElapsedMilliseconds}ms across {iterations} iterations\n" +
                $"// Average: {(double)sw.ElapsedMilliseconds / iterations}ms");
        }

        private static void BenchmarkDecoder(string fileName, int iterations)
        {
            string loadPath = String.Format(pathTemplate, fileName);

            using var fileStream = new FileStream(loadPath, FileMode.Open);
            using var inputStream = new MemoryStream();
            fileStream.CopyTo(inputStream);

            var decoder = new JpegDecoder { IgnoreMetadata = true };

            var sw = new Stopwatch();
            sw.Start();
            for (int i = 0; i < iterations; i++)
            {
                inputStream.Position = 0;
                using Image img = decoder.Decode(Configuration.Default, inputStream, CancellationToken.None);
            }
            sw.Stop();

            Console.WriteLine($"// Decoding\n" +
                $"// Elapsed: {sw.ElapsedMilliseconds}ms across {iterations} iterations\n" +
                $"// Average: {(double)sw.ElapsedMilliseconds / iterations}ms");
        }

        private static void BenchmarkResizingLoop__experimental(string fileName, Size targetSize, int iterations)
        {
            string loadPath = String.Format(pathTemplate, fileName);

            using var fileStream = new FileStream(loadPath, FileMode.Open);
            using var saveStream = new MemoryStream();
            using var inputStream = new MemoryStream();
            fileStream.CopyTo(inputStream);

            var decoder = new JpegDecoder { IgnoreMetadata = true };
            var encoder = new JpegEncoder { ColorType = JpegColorType.YCbCrRatio444 };

            var sw = new Stopwatch();
            sw.Start();
            for (int i = 0; i < iterations; i++)
            {
                inputStream.Position = 0;
                using Image img = decoder.Experimental__DecodeInto<Rgb24>(Configuration.Default, inputStream, targetSize, CancellationToken.None);
                img.SaveAsJpeg(saveStream, encoder);
            }
            sw.Stop();

            Console.WriteLine($"// Decode-Resize-Encode w/ downscaling decoder\n" +
                $"// Elapsed: {sw.ElapsedMilliseconds}ms across {iterations} iterations\n" +
                $"// Average: {(double)sw.ElapsedMilliseconds / iterations}ms");
        }

        private static void BenchmarkResizingLoop__explicit(string fileName, Size targetSize, int iterations)
        {
            string loadPath = String.Format(pathTemplate, fileName);

            using var fileStream = new FileStream(loadPath, FileMode.Open);
            using var saveStream = new MemoryStream();
            using var inputStream = new MemoryStream();
            fileStream.CopyTo(inputStream);

            var decoder = new JpegDecoder { IgnoreMetadata = true };
            var encoder = new JpegEncoder { ColorType = JpegColorType.YCbCrRatio444 };

            var sw = new Stopwatch();
            sw.Start();
            for (int i = 0; i < iterations; i++)
            {
                inputStream.Position = 0;
                using Image img = decoder.Decode<Rgb24>(Configuration.Default, inputStream, CancellationToken.None);
                img.Mutate(ctx => ctx.Resize(targetSize, KnownResamplers.Box, false));
                img.SaveAsJpeg(saveStream, encoder);
            }
            sw.Stop();

            Console.WriteLine($"// Decode-Resize-Encode w/ Mutate()\n" +
                $"// Elapsed: {sw.ElapsedMilliseconds}ms across {iterations} iterations\n" +
                $"// Average: {(double)sw.ElapsedMilliseconds / iterations}ms");
        }

        private static void ReEncodeImage(string fileName, int? quality = null)
        {
            string loadPath = String.Format(pathTemplate, fileName);
            using Image img = Image.Load(loadPath);

            string savePath = String.Format(pathTemplate, $"q{quality}_test_{fileName}");
            var encoder = new JpegEncoder()
            {
                Quality = quality,
                ColorType = JpegColorType.YCbCrRatio444
            };
            img.SaveAsJpeg(savePath, encoder);
        }

        private static void DecodeImageResize__explicit(string fileName, Size targetSize, int? quality = null)
        {
            string loadPath = String.Format(pathTemplate, fileName);
            string savePath = String.Format(pathTemplate, $"q{quality}_test_{fileName}");

            var decoder = new JpegDecoder();
            var encoder = new JpegEncoder()
            {
                Quality = quality,
                ColorType = JpegColorType.YCbCrRatio444
            };

            using Image img = decoder.Decode<Rgb24>(Configuration.Default, File.OpenRead(loadPath), CancellationToken.None);
            img.Mutate(ctx => ctx.Resize(targetSize, KnownResamplers.Box, false));
            img.SaveAsJpeg(savePath, encoder);

        }

        private static void DecodeImageResize__experimental(string fileName, Size targetSize, int? quality = null)
        {
            string loadPath = String.Format(pathTemplate, fileName);

            var decoder = new JpegDecoder();
            using Image img = decoder.Experimental__DecodeInto<Rgb24>(Configuration.Default, File.OpenRead(loadPath), targetSize, CancellationToken.None);

            string savePath = String.Format(pathTemplate, $"q{quality}_test_{fileName}");
            var encoder = new JpegEncoder()
            {
                Quality = quality,
                ColorType = JpegColorType.YCbCrRatio444
            };
            img.SaveAsJpeg(savePath, encoder);
        }

        private static Version GetNetCoreVersion()
        {
            Assembly assembly = typeof(System.Runtime.GCSettings).GetTypeInfo().Assembly;
            Console.WriteLine(assembly.Location);
            string[] assemblyPath = assembly.Location.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            int netCoreAppIndex = Array.IndexOf(assemblyPath, "Microsoft.NETCore.App");
            if (netCoreAppIndex > 0 && netCoreAppIndex < assemblyPath.Length - 2)
            {
                return Version.Parse(assemblyPath[netCoreAppIndex + 1]);
            }

            return null;
        }

        private static void RunJpegEncoderProfilingTests()
        {
            var benchmarks = new JpegProfilingBenchmarks(new ConsoleOutput());
            benchmarks.EncodeJpeg_SingleMidSize();
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
