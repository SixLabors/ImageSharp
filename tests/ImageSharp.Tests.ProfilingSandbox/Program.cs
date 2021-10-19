using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
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
            //BenchmarkEncoder("snow_main", 200, 100, JpegColorType.YCbCrRatio444);
            //BenchmarkEncoder("snow_main", 200, 90, JpegColorType.YCbCrRatio444);
            //BenchmarkEncoder("snow_main", 200, 75, JpegColorType.YCbCrRatio444);
            //BenchmarkEncoder("snow_main", 200, 50, JpegColorType.YCbCrRatio444);

            //BenchmarkEncoder("snow_main", 200, 100, JpegColorType.YCbCrRatio420);
            //BenchmarkEncoder("snow_main", 200, 90, JpegColorType.YCbCrRatio420);
            //BenchmarkEncoder("snow_main", 200, 75, JpegColorType.YCbCrRatio420);
            //BenchmarkEncoder("snow_main", 200, 50, JpegColorType.YCbCrRatio420);

            //BenchmarkEncoder("snow_main", 200, 100, JpegColorType.Luminance);
            //BenchmarkEncoder("snow_main", 200, 90, JpegColorType.Luminance);
            //BenchmarkEncoder("snow_main", 200, 75, JpegColorType.Luminance);
            //BenchmarkEncoder("snow_main", 200, 50, JpegColorType.Luminance);

            /// Transposing ZigZag
            // Elapsed: 6758ms across 400 iterations
            // Average: 16,895ms
            //BenchmarkDecoder("master", 400);

            //ReEncodeImage("master", 100);
            //ReEncodeImage("master", 90);
            //ReEncodeImage("master", 75);
            //ReEncodeImage("master", 50);

            TestGenerationalLoss("tree", 5);
            TestGenerationalLoss("tree", 25);
            TestGenerationalLoss("tree", 50);
            TestGenerationalLoss("tree", 500);

            Console.WriteLine("Done.");
        }

        const string pathTemplate = "C:\\Users\\pl4nu\\Downloads\\{0}.jpg";

        private static void TestGenerationalLoss(string fileName, int generations)
        {
            const string savePathTemplate = "C:\\Users\\pl4nu\\Downloads\\{0}_gen{1}.jpg";

            var sw = new Stopwatch();
            sw.Start();

            using var memoryBuffer = new MemoryStream();
            using (var initial = Image.Load(string.Format(pathTemplate, fileName)))
            {
                initial.SaveAsJpeg(memoryBuffer);
                memoryBuffer.Position = 0;
            }

            for (int i = 0; i < generations; i++)
            {
                using var img = Image.Load(memoryBuffer);
                memoryBuffer.Position = 0;

                img.SaveAsJpeg(memoryBuffer);
                memoryBuffer.Position = 0;
            }

            using FileStream fileStream = File.Create(string.Format(savePathTemplate, fileName, generations));
            memoryBuffer.CopyTo(fileStream);

            sw.Stop();
            Console.WriteLine($"// Spent {sw.ElapsedMilliseconds}ms for {generations} generations\n");
        }

        private static void BenchmarkEncoder(string fileName, int iterations, int quality, JpegColorType color)
        {
            string loadPath = String.Format(pathTemplate, fileName);

            using var inputStream = new FileStream(loadPath, FileMode.Open);
            using var saveStream = new MemoryStream();

            var decoder = new JpegDecoder { IgnoreMetadata = true };
            using Image img = decoder.Decode(Configuration.Default, inputStream);

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

            using var memoryStream = new MemoryStream();
            using (var fileStream = new FileStream(loadPath, FileMode.Open))
            {
                fileStream.CopyTo(memoryStream);
            }

            var decoder = new JpegDecoder { IgnoreMetadata = true };

            var sw = new Stopwatch();
            sw.Start();
            for (int i = 0; i < iterations; i++)
            {
                memoryStream.Position = 0;
                using Image img = decoder.Decode(Configuration.Default, memoryStream);
            }
            sw.Stop();

            Console.WriteLine($"// Decoding\n" +
                $"// Elapsed: {sw.ElapsedMilliseconds}ms across {iterations} iterations\n" +
                $"// Average: {(double)sw.ElapsedMilliseconds / iterations}ms");
        } 

        private static void ReEncodeImage(string fileName, int quality)
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

        private static void ReEncodeToPNG(string fileName)
        {
            string loadPath = String.Format(pathTemplate, fileName);
            using Image img = Image.Load(loadPath);

            string savePath = String.Format(pathTemplate, $"png_test_{fileName}");
            var encoder = new PngEncoder()
            {
                CompressionLevel = PngCompressionLevel.NoCompression
            };
            img.SaveAsPng(savePath, encoder);
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
