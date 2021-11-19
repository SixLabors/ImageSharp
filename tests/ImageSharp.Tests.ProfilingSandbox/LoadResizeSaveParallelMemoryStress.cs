// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using CommandLine;
using CommandLine.Text;
using SixLabors.ImageSharp.Benchmarks.LoadResizeSave;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Memory.Internals;

namespace SixLabors.ImageSharp.Tests.ProfilingSandbox
{
    // See ImageSharp.Benchmarks/LoadResizeSave/README.md
    internal class LoadResizeSaveParallelMemoryStress
    {
        private LoadResizeSaveParallelMemoryStress()
        {
            this.Benchmarks = new LoadResizeSaveStressRunner()
            {
                Filter = JpegKind.Baseline,
            };
            this.Benchmarks.Init();
        }

        public LoadResizeSaveStressRunner Benchmarks { get; }

        public static void Run(string[] args)
        {
            Console.WriteLine($"Running: {typeof(LoadResizeSaveParallelMemoryStress).Assembly.Location}");
            Console.WriteLine($"64 bit: {Environment.Is64BitProcess}");
            var options = args.Length > 0 ? CommandLineOptions.Parse(args) : null;

            var lrs = new LoadResizeSaveParallelMemoryStress();
            if (options != null)
            {
                lrs.Benchmarks.MaxDegreeOfParallelism = options.MaxDegreeOfParallelism;
            }

            Console.WriteLine($"\nEnvironment.ProcessorCount={Environment.ProcessorCount}");
            Stopwatch timer;

            if (options == null || !options.ImageSharp)
            {
                RunBenchmarkSwitcher(lrs, out timer);
            }
            else
            {
                Console.WriteLine("Running ImageSharp with options:");
                Console.WriteLine(options.ToString());

                if (!options.KeepDefaultAllocator)
                {
                    MemoryAllocator.Default = Configuration.Default.MemoryAllocator = options.CreateMemoryAllocator();
                }

                lrs.Benchmarks.EmulateLeakedAllocations = options.LeakAllocations;

                timer = Stopwatch.StartNew();
                try
                {
                    for (int i = 0; i < options.RepeatCount; i++)
                    {
                        lrs.ImageSharpBenchmarkParallel();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

                timer.Stop();

                if (options.ReleaseRetainedResourcesAtEnd)
                {
                    Configuration.Default.MemoryAllocator.ReleaseRetainedResources();
                }

                for (int i = 0; i < options.FinalGcCount; i++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    Thread.Sleep(1000);
                }
            }

            var stats = new Stats(timer, lrs.Benchmarks.TotalProcessedMegapixels);
            Console.WriteLine($"Total Megapixels: {stats.TotalMegapixels}, TotalOomRetries: {UnmanagedMemoryHandle.TotalOomRetries}");
            Console.WriteLine(stats.GetMarkdown());
            if (options?.FileOutput != null)
            {
                PrintFileOutput(options.FileOutput, stats);
            }

            if (options != null && options.PauseAtEnd)
            {
                Console.WriteLine("Press ENTER");
                Console.ReadLine();
            }
        }

        private static void PrintFileOutput(string fileOutput, Stats stats)
        {
            string[] ss = fileOutput.Split(';');
            string fileName = ss[0];
            string content = ss[1]
                .Replace("TotalSeconds", stats.TotalSeconds.ToString(CultureInfo.InvariantCulture))
                .Replace("EOL", Environment.NewLine);
            File.AppendAllText(fileName, content);
        }

        private static void RunBenchmarkSwitcher(LoadResizeSaveParallelMemoryStress lrs, out Stopwatch timer)
        {
            Console.WriteLine(@"Choose a library for image resizing stress test:

1. System.Drawing
2. ImageSharp
3. MagicScaler
4. SkiaSharp
5. NetVips
6. ImageMagick
");

            ConsoleKey key = Console.ReadKey().Key;
            if (key < ConsoleKey.D1 || key > ConsoleKey.D6)
            {
                Console.WriteLine("Unrecognized command.");
                Environment.Exit(-1);
            }

            timer = Stopwatch.StartNew();

            switch (key)
            {
                case ConsoleKey.D1:
                    lrs.SystemDrawingBenchmarkParallel();
                    break;
                case ConsoleKey.D2:
                    lrs.ImageSharpBenchmarkParallel();
                    break;
                case ConsoleKey.D3:
                    lrs.MagicScalerBenchmarkParallel();
                    break;
                case ConsoleKey.D4:
                    lrs.SkiaBitmapBenchmarkParallel();
                    break;
                case ConsoleKey.D5:
                    lrs.NetVipsBenchmarkParallel();
                    break;
                case ConsoleKey.D6:
                    lrs.MagickBenchmarkParallel();
                    break;
            }

            timer.Stop();
        }

        private struct Stats
        {
            public double TotalSeconds { get; }

            public double TotalMegapixels { get; }

            public double MegapixelsPerSec { get; }

            public double MegapixelsPerSecPerCpu { get; }

            public Stats(Stopwatch sw, double totalMegapixels)
            {
                this.TotalMegapixels = totalMegapixels;
                this.TotalSeconds = sw.ElapsedMilliseconds / 1000.0;
                this.MegapixelsPerSec = totalMegapixels / this.TotalSeconds;
                this.MegapixelsPerSecPerCpu = this.MegapixelsPerSec / Environment.ProcessorCount;
            }

            public string GetMarkdown()
            {
                var bld = new StringBuilder();
                bld.AppendLine($"| {nameof(this.TotalSeconds)} | {nameof(this.MegapixelsPerSec)} | {nameof(this.MegapixelsPerSecPerCpu)} |");
                bld.AppendLine(
                    $"| {L(nameof(this.TotalSeconds))} | {L(nameof(this.MegapixelsPerSec))} | {L(nameof(this.MegapixelsPerSecPerCpu))} |");

                bld.Append("| ");
                bld.AppendFormat(F(nameof(this.TotalSeconds)), this.TotalSeconds);
                bld.Append(" | ");
                bld.AppendFormat(F(nameof(this.MegapixelsPerSec)), this.MegapixelsPerSec);
                bld.Append(" | ");
                bld.AppendFormat(F(nameof(this.MegapixelsPerSecPerCpu)), this.MegapixelsPerSecPerCpu);
                bld.AppendLine(" |");

                return bld.ToString();

                static string L(string header) => new('-', header.Length);
                static string F(string column) => $"{{0,{column.Length}:f3}}";
            }
        }

        private class CommandLineOptions
        {
            [Option('i', "imagesharp", Required = false, Default = false, HelpText = "Test ImageSharp without benchmark switching")]
            public bool ImageSharp { get; set; }

            [Option('d', "default-allocator", Required = false, Default = false, HelpText = "Keep default MemoryAllocator and ignore all settings")]
            public bool KeepDefaultAllocator { get; set; }

            [Option('m', "max-contiguous", Required = false, Default = 4, HelpText = "Maximum size of contiguous pool buffers in MegaBytes")]
            public int MaxContiguousPoolBufferMegaBytes { get; set; } = 4;

            [Option('s', "poolsize", Required = false, Default = 4096, HelpText = "The size of the pool in MegaBytes")]
            public int MaxPoolSizeMegaBytes { get; set; } = 4096;

            [Option('u', "max-nonpool", Required = false, Default = 32, HelpText = "Maximum size of non-pooled contiguous blocks in MegaBytes")]
            public int MaxCapacityOfNonPoolBuffersMegaBytes { get; set; } = 32;

            [Option('p', "parallelism", Required = false, Default = -1, HelpText = "Level of parallelism")]
            public int MaxDegreeOfParallelism { get; set; } = -1;

            [Option('r', "repeat-count", Required = false, Default = 1, HelpText = "Times to run the whole benchmark")]
            public int RepeatCount { get; set; } = 1;

            [Option('g', "final-gc-count", Required = false, Default = 0, HelpText = "How many times to GC.Collect after execution")]
            public int FinalGcCount { get; set; }

            [Option('e', "release-at-end", Required = false, Default = false, HelpText = "Specify to run ReleaseRetainedResources() after execution")]
            public bool ReleaseRetainedResourcesAtEnd { get; set; }

            [Option('w', "pause", Required = false, Default = false, HelpText = "Specify to pause and wait for user input after the execution")]
            public bool PauseAtEnd { get; set; }

            [Option('f', "file", Required = false, Default = null, HelpText = "Specify to print the execution time to a file. Format: '<file>;<formatstr>' see the code for formatstr semantics.")]
            public string FileOutput { get; set; }

            [Option('t', "trim-period", Required = false, Default = null, HelpText = "Trim period for the pool in seconds")]
            public int? TrimTimeSeconds { get; set; }

            [Option('l', "leak-allocations", Required = false, Default = false, HelpText = "Inject leaking memory allocation requests to stress-test finalizer behavior.")]
            public bool LeakAllocations { get; set; }

            public static CommandLineOptions Parse(string[] args)
            {
                CommandLineOptions result = null;
                ParserResult<CommandLineOptions> parserResult = Parser.Default.ParseArguments<CommandLineOptions>(args).WithParsed(o =>
                {
                    result = o;
                });

                if (result == null)
                {
                    Console.WriteLine(HelpText.RenderUsageText(parserResult));
                }

                return result;
            }

            public override string ToString() =>
                $"p({this.MaxDegreeOfParallelism})_i({this.ImageSharp})_d({this.KeepDefaultAllocator})_m({this.MaxContiguousPoolBufferMegaBytes})_s({this.MaxPoolSizeMegaBytes})_u({this.MaxCapacityOfNonPoolBuffersMegaBytes})_r({this.RepeatCount})_g({this.FinalGcCount})_e({this.ReleaseRetainedResourcesAtEnd}_l({this.LeakAllocations}))";

            public MemoryAllocator CreateMemoryAllocator()
            {
                if (this.TrimTimeSeconds.HasValue)
                {
                    return new UniformUnmanagedMemoryPoolMemoryAllocator(
                        1024 * 1024,
                        (int)B(this.MaxContiguousPoolBufferMegaBytes),
                        B(this.MaxPoolSizeMegaBytes),
                        (int)B(this.MaxCapacityOfNonPoolBuffersMegaBytes),
                        new UniformUnmanagedMemoryPool.TrimSettings
                        {
                            TrimPeriodMilliseconds = this.TrimTimeSeconds.Value * 1000
                        });
                }
                else
                {
                    return new UniformUnmanagedMemoryPoolMemoryAllocator(
                        1024 * 1024,
                        (int)B(this.MaxContiguousPoolBufferMegaBytes),
                        B(this.MaxPoolSizeMegaBytes),
                        (int)B(this.MaxCapacityOfNonPoolBuffersMegaBytes));
                }
            }

            private static long B(double megaBytes) => (long)(megaBytes * 1024 * 1024);
        }

        private void ForEachImage(Action<string> action) => this.Benchmarks.ForEachImageParallel(action);

        private void SystemDrawingBenchmarkParallel() => this.ForEachImage(this.Benchmarks.SystemDrawingResize);

        private void ImageSharpBenchmarkParallel()
        {
            int cnt = 0;
            this.ForEachImage(f =>
            {
                this.Benchmarks.ImageSharpResize(f);
                if (cnt % 4 == 0 && this.Benchmarks.EmulateLeakedAllocations)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }

                cnt++;
            });
        }

        private void MagickBenchmarkParallel() => this.ForEachImage(this.Benchmarks.MagickResize);

        private void MagicScalerBenchmarkParallel() => this.ForEachImage(this.Benchmarks.MagicScalerResize);

        private void SkiaBitmapBenchmarkParallel() => this.ForEachImage(this.Benchmarks.SkiaBitmapResize);

        private void NetVipsBenchmarkParallel() => this.ForEachImage(this.Benchmarks.NetVipsResize);
    }
}
