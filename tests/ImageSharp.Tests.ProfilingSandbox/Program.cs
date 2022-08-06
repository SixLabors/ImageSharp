// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using PhotoSauce.MagicScaler;
using PhotoSauce.MagicScaler.Interpolators;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder;
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

        const string pathTemplate = "C:\\Users\\pl4nu\\Downloads\\{0}.jpg";

        public static void Main(string[] args)
        {
            //string imageName = "Calliphora_aligned_size";
            //string imageName = "Calliphora";
            string imageName = "1x1";
            //string imageName = "bw_check";
            //string imageName = "bw_check_color";
            ReEncodeImage(imageName, JpegEncodingColor.YCbCrRatio444, 100);
            ReEncodeImage(imageName, JpegEncodingColor.YCbCrRatio422, 100);
            ReEncodeImage(imageName, JpegEncodingColor.YCbCrRatio420, 100);
            ReEncodeImage(imageName, JpegEncodingColor.YCbCrRatio411, 100);
            ReEncodeImage(imageName, JpegEncodingColor.YCbCrRatio410, 100);
            //ReEncodeImage(imageName, JpegEncodingColor.Luminance, 100);
            //ReEncodeImage(imageName, JpegEncodingColor.Rgb, 100);
            //ReEncodeImage(imageName, JpegEncodingColor.Cmyk, 100);

            // Encoding q=75 | color=YCbCrRatio444
            // Elapsed: 4901ms across 500 iterations
            // Average: 9,802ms
            //BenchmarkEncoder(imageName, 500, 75, JpegEncodingColor.YCbCrRatio444);
        }

        private static void BenchmarkEncoder(string fileName, int iterations, int quality, JpegEncodingColor color)
        {
            string loadPath = String.Format(pathTemplate, fileName);

            using var inputStream = new FileStream(loadPath, FileMode.Open);
            using var saveStream = new MemoryStream();

            var decoder = new JpegDecoder { IgnoreMetadata = true };
            using Image img = decoder.Decode(Configuration.Default, inputStream, CancellationToken.None);

            var encoder = new JpegEncoder()
            {
                Quality = quality,
                ColorType = color,
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

        private static void BenchmarkResizingLoop__explicit(string fileName, Size targetSize, int iterations)
        {
            string loadPath = String.Format(pathTemplate, fileName);

            using var fileStream = new FileStream(loadPath, FileMode.Open);
            using var saveStream = new MemoryStream();
            using var inputStream = new MemoryStream();
            fileStream.CopyTo(inputStream);

            var decoder = new JpegDecoder { IgnoreMetadata = true };
            var encoder = new JpegEncoder { ColorType = JpegEncodingColor.YCbCrRatio444 };

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

        private static void ReEncodeImage(string fileName, JpegEncodingColor mode, int? quality = null)
        {
            string loadPath = String.Format(pathTemplate, fileName);
            using Image img = Image.Load(loadPath);

            string savePath = String.Format(pathTemplate, $"q{quality}_{mode}_test_{fileName}");
            var encoder = new JpegEncoder()
            {
                Quality = quality,
                ColorType = mode,
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
                ColorType = JpegEncodingColor.YCbCrRatio444
            };

            using Image img = decoder.Decode<Rgb24>(Configuration.Default, File.OpenRead(loadPath), CancellationToken.None);
            img.Mutate(ctx => ctx.Resize(targetSize, sampler, compand: false));
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
