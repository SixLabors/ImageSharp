// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using PhotoSauce.MagicScaler;
using PhotoSauce.MagicScaler.Interpolators;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
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

        const string pathTemplate = ..."";

        // Second pass - must be 5% smaller than appropriate IDCT scaled size
        const float scale = 0.75f;
        readonly IResampler resampler = KnownResamplers.Box;

        public static void Main(string[] args)
        {
            //ReEncodeImage("jpeg444");

            //Size targetSize = new Size(808, 1200);

            // 808 x 1200
            // 404 x 600
            // 202 x 300
            // 101 x 150
            string imageName = "Calliphora_aligned_size";

            // Exact matches for 8/4/2/1 scaling
            //Size exactSizeX8 = new Size(808, 1200);
            //Size exactSizeX4 = new Size(404, 600);
            //Size exactSizeX2 = new Size(202, 300);
            //Size exactSizeX1 = new Size(101, 150);
            //ReencodeImageResize__experimental(imageName, exactSizeX8);
            //ReencodeImageResize__experimental(imageName, exactSizeX4);
            //ReencodeImageResize__experimental(imageName, exactSizeX2);
            //ReencodeImageResize__experimental(imageName, exactSizeX1);

            Size secondPassSizeX8 = new Size((int)(808 * scale), (int)(1200 * scale));
            Size secondPassSizeX4 = new Size((int)(404 * scale), (int)(600 * scale));
            Size secondPassSizeX2 = new Size((int)(202 * scale), (int)(300 * scale));
            Size secondPassSizeX1 = new Size((int)(101 * scale), (int)(150 * scale));
            ReencodeImageResize__experimental(imageName, secondPassSizeX8);
            ReencodeImageResize__experimental(imageName, secondPassSizeX4);
            ReencodeImageResize__experimental(imageName, secondPassSizeX2);
            ReencodeImageResize__experimental(imageName, secondPassSizeX1);
            ReencodeImageResize__explicit(imageName, secondPassSizeX8, resampler);
            ReencodeImageResize__explicit(imageName, secondPassSizeX4, resampler);
            ReencodeImageResize__explicit(imageName, secondPassSizeX2, resampler);
            ReencodeImageResize__explicit(imageName, secondPassSizeX1, resampler);

            // 'native' resizing - only jpeg dct downscaling
            //ReencodeImageResize_Comparison("Calliphora_ratio1", targetSize, 100);

            // 'native' + software resizing - jpeg dct downscaling + postprocessing
            //ReencodeImageResize_Comparison("Calliphora_aligned_size", new Size(269, 400), 99);

            //var benchmarkSize = new Size(404, 600);
            //BenchmarkResizingLoop__explicit("jpeg_quality_100", benchmarkSize, 300);
            //BenchmarkResizingLoop__experimental("jpeg_quality_100", benchmarkSize, 300);

            Console.WriteLine("Done.");
        }

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

        private static void ReencodeImageResize__explicit(string fileName, Size targetSize, IResampler sampler, int? quality = null)
        {
            string loadPath = String.Format(pathTemplate, fileName);
            string savePath = String.Format(pathTemplate, $"is_res_{sampler.GetType().Name}[{targetSize.Width}x{targetSize.Height}]_{fileName}");

            var decoder = new JpegDecoder();
            var encoder = new JpegEncoder()
            {
                Quality = quality,
                ColorType = JpegColorType.YCbCrRatio444
            };

            using Image img = decoder.Decode<Rgb24>(Configuration.Default, File.OpenRead(loadPath), CancellationToken.None);
            img.Mutate(ctx => ctx.Resize(targetSize, sampler, compand: false));
            img.SaveAsJpeg(savePath, encoder);
        }

        private static void ReencodeImageResize__experimental(string fileName, Size targetSize, int? quality = null)
        {
            string loadPath = String.Format(pathTemplate, fileName);
            string savePath = String.Format(pathTemplate, $"is_res_jpeg[{targetSize.Width}x{targetSize.Height}]_{fileName}");

            var decoder = new JpegDecoder { IgnoreMetadata = true };
            using Image img = decoder.Experimental__DecodeInto<Rgb24>(Configuration.Default, File.OpenRead(loadPath), targetSize, CancellationToken.None);

            var encoder = new JpegEncoder()
            {
                Quality = quality,
                ColorType = JpegColorType.YCbCrRatio444
            };
            img.SaveAsJpeg(savePath, encoder);
        }

        private static void ReencodeImageResize__Netvips(string fileName, Size targetSize, int? quality)
        {
            string loadPath = String.Format(pathTemplate, fileName);
            string savePath = String.Format(pathTemplate, $"netvips_resize_{fileName}");

            using var thumb = NetVips.Image.Thumbnail(loadPath, targetSize.Width, targetSize.Height);

            // Save the results
            thumb.Jpegsave(savePath, q: quality, strip: true, subsampleMode: NetVips.Enums.ForeignSubsample.Off);
        }

        private static void ReencodeImageResize__MagicScaler(string fileName, Size targetSize, int quality)
        {
            string loadPath = String.Format(pathTemplate, fileName);
            string savePath = String.Format(pathTemplate, $"magicscaler_resize_{fileName}");

            var settings = new ProcessImageSettings()
            {
                Width = targetSize.Width,
                Height = targetSize.Height,
                SaveFormat = FileFormat.Jpeg,
                JpegQuality = quality,
                JpegSubsampleMode = ChromaSubsampleMode.Subsample444,
                Sharpen = false,
                ColorProfileMode = ColorProfileMode.Ignore,
                HybridMode = HybridScaleMode.Turbo,
            };

            using var output = new FileStream(savePath, FileMode.Create);
            MagicImageProcessor.ProcessImage(loadPath, output, settings);
        }

        private static void ReencodeImageResize_Comparison(string fileName, Size targetSize, int quality)
        {
            ReencodeImageResize__experimental(fileName, targetSize, quality);
            ReencodeImageResize__Netvips(fileName, targetSize, quality);
            ReencodeImageResize__MagicScaler(fileName, targetSize, quality);
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
