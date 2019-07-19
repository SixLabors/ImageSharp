// <copyright file="Program.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

using System.Diagnostics;

using SixLabors.ImageSharp.Tests.PixelFormats.PixelOperations;
using SixLabors.ImageSharp.Tests.ProfilingBenchmarks;

namespace SixLabors.ImageSharp.Sandbox46
{
    using System;
    using System.IO;
    using SixLabors.ImageSharp.Formats.Bmp;
    using SixLabors.ImageSharp.PixelFormats;
    using SixLabors.ImageSharp.Processing;
    using SixLabors.ImageSharp.Processing.Processors.Normalization;
    using SixLabors.ImageSharp.Tests;
    using SixLabors.ImageSharp.Tests.Formats.Jpg;
    using SixLabors.ImageSharp.Tests.PixelFormats;
    using SixLabors.ImageSharp.Tests.Processing.Processors.Transforms;
    using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;
    using Xunit.Abstractions;

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
            string path = Directory.GetCurrentDirectory();
            Console.WriteLine(path);

            var decoder = new BmpDecoder()
            {
                RleSkippedPixelHandling = RleSkippedPixelHandling.Black
            };
            //using (var image = Image.Load(@"E:\testimages\bmpsuite-2.5\bmpsuite-2.5\x\ba-bm.bmp", decoder))
            using (var image = Image.Load(@"E:\testimages\png\PngWithText.png"))
            //using (var image = Image.Load(@"E:\testimages\bmp\OS2Icons\VISAGEIC\VisualAge C++ Tools Icons\Boxes.ICO"))
            {
                image.Save("input.png");
                //image.Save("input.jpg");
                //image.Save("input.bmp");

                //image.Save("output.jpg");
                image.Save("output.bmp", new BmpEncoder()
                {
                    BitsPerPixel = BmpBitsPerPixel.Pixel32,
                    SupportTransparency = true
                });

                image.Save("output.png");
            }

            /*Image<Rgba32> image2 = Image.Load(@"C:\Users\brian\Downloads\TestImages\Test-original.bmp");
            using (FileStream output = File.OpenWrite("Issue_732.bmp"))
            {
                //image2.Mutate(img => img.Resize(image2.Width / 2, image2.Height / 2));
                image2.SaveAsBmp(output);
            }*/
            //CompareTwoImages(expected: @"E:\testimages\bmp\rgba32-1010102-gimp3.png", actual: @"E:\testimages\bmp\rgba32-1010102.bmp");
            //CompareImagesToRefernceInDir();
            //ProcessImagesInDir();
            //RunJpegColorProfilingTests();
            // RunDecodeJpegProfilingTests();
            // RunToVector4ProfilingTest();
            //RunResizeProfilingTest();

            Console.WriteLine("done");
            Console.ReadLine();
        }

        private static void ProcessImagesInDir()
        {
            string path = Directory.GetCurrentDirectory();
            Console.WriteLine(path);
            var files = Directory.EnumerateFiles(@"E:\testimages\PngSuite-2017jul19", "*.png");
            //var files = Directory.EnumerateFiles(@"E:\testimages\bmp\bitmap_array", "*.bmp");
            int counter = 0;
            var failedDir = "failedDir";
            Directory.CreateDirectory(failedDir);
            foreach (var file in files)
            {
                try
                {
                    using (var image = Image.Load(file))
                    {
                        image.Save(Path.GetFileName(file + ".bmp"));
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"could not load image: {file}");
                    string fileName = Path.Combine(failedDir, Path.GetFileName(file));
                    if (!File.Exists(fileName))
                    {
                        File.Copy(file, fileName);
                    }
                }
                counter++;
            }
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
            var tests = new PixelOperationsTests.Rgba32OperationsTests(new ConsoleOutput());
            tests.Benchmark_ToVector4();
        }

        private static void RunDecodeJpegProfilingTests()
        {
            Console.WriteLine("RunDecodeJpegProfilingTests...");
            var benchmarks = new JpegProfilingBenchmarks(new ConsoleOutput());
            foreach (object[] data in JpegProfilingBenchmarks.DecodeJpegData)
            {
                string fileName = (string)data[0];
                benchmarks.DecodeJpeg(fileName);
            }
        }
    }
}
