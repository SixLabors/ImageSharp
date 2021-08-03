// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime;
using System.Text;
using System.Threading;
using CommandLine;
using SixLabors.ImageSharp.Benchmarks.LoadResizeSave;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Tests.ProfilingSandbox
{
    // See ImageSharp.Benchmarks/LoadResizeSave/README.md
    internal class LoadResizeSaveParallelMemoryStress
    {
        private LoadResizeSaveParallelMemoryStress()
        {
            this.Benchmarks = new LoadResizeSaveStressRunner()
            {
                // MaxDegreeOfParallelism = 10,
                // Filter = JpegKind.Baseline
            };
            this.Benchmarks.Init();
        }

        public LoadResizeSaveStressRunner Benchmarks { get; }

        public static void Run(string[] args)
        {
            var options = CommandLineOptions.Parse(args);

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
                Configuration.Default.MemoryAllocator = options.CreateMemoryAllocator();
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
                    if (options.CompactLohOnFinalGc)
                    {
                        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                    }

                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    Thread.Sleep(1000);
                }
            }

            var stats = new Stats(timer, lrs.Benchmarks.TotalProcessedMegapixels);
            Console.WriteLine(stats.GetMarkdown());
            if (options != null && options.PauseAtEnd)
            {
                Console.WriteLine("Press ENTER");
                Console.ReadLine();
            }
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

                static string L(string header) => new ('-', header.Length);
                static string F(string column) => $"{{0,{column.Length}:f3}}";
            }
        }

        private class CommandLineOptions
        {
            [Option('i', "imagesharp", Required = false, Default = false)]
            public bool ImageSharp { get; set; }

            [Option('a', "arraypool-alloc", Required = false, Default = false)]
            public bool UseArrayPoolMemoryAllocator { get; set; }

            [Option('d', "default-alloc", Required = false, Default = false)]
            public bool UseDefaultAllocatorWithDefaultSettings { get; set; }

            [Option('m', "max-managed", Required = false, Default = 4)]
            public int MaxContiguousPoolBufferMegaBytes { get; set; } = 4;

            [Option('s', "poolsize", Required = false, Default = 4096)]
            public int MaxPoolSizeMegaBytes { get; set; } = 4096;

            [Option('u', "max-unmg", Required = false, Default = 32)]
            public int MaxCapacityOfUnmanagedBuffersMegaBytes { get; set; } = 32;

            [Option('p', "parallelism", Required = false, Default = -1)]
            public int MaxDegreeOfParallelism { get; set; } = -1;

            [Option('t', "trimrate", Required = false, Default = 0.5)]
            public double TrimRate { get; set; } = 0.5;

            [Option('r', "repeat-count", Required = false, Default = 1)]
            public int RepeatCount { get; set; } = 1;

            // This is to test trimming and virtual memory decommit
            [Option('g', "final-gc-count", Required = false, Default = 0)]
            public int FinalGcCount { get; set; }

            [Option('l', "compact-loh-on-final-gc", Required = false, Default = false)]
            public bool CompactLohOnFinalGc { get; set; }

            [Option('e', "release-at-end", Required = false, Default = false)]
            public bool ReleaseRetainedResourcesAtEnd { get; set; }

            [Option('w', "pause", Required = false, Default = false)]
            public bool PauseAtEnd { get; set; }

            public static CommandLineOptions Parse(string[] args)
            {
                CommandLineOptions result = null;
                Parser.Default.ParseArguments<CommandLineOptions>(args).WithParsed(o =>
                {
                    result = o;
                });
                return result;
            }

            public override string ToString() =>
                $"p({this.MaxDegreeOfParallelism})_i({this.ImageSharp})_a({this.UseArrayPoolMemoryAllocator})_d({this.UseDefaultAllocatorWithDefaultSettings})_m({this.MaxContiguousPoolBufferMegaBytes})_s({this.MaxPoolSizeMegaBytes})_u({this.MaxCapacityOfUnmanagedBuffersMegaBytes})_t({this.TrimRate})_r({this.RepeatCount})_g({this.FinalGcCount})_l({this.CompactLohOnFinalGc})_e({this.ReleaseRetainedResourcesAtEnd})";

            public MemoryAllocator CreateMemoryAllocator()
            {
                if (this.UseArrayPoolMemoryAllocator)
                {
                    return ArrayPoolMemoryAllocator.CreateDefault();
                }

                if (this.UseDefaultAllocatorWithDefaultSettings)
                {
                    return MemoryAllocator.CreateDefault();
                }

                return new UniformByteArrayPoolMemoryAllocator(
                    1024 * 1024,
                    (int)B(this.MaxContiguousPoolBufferMegaBytes),
                    B(this.MaxPoolSizeMegaBytes),
                    (int)B(this.MaxCapacityOfUnmanagedBuffersMegaBytes),
                    (float)this.TrimRate);
            }

            private static long B(double megaBytes) => (long)(megaBytes * 1024 * 1024);
        }

        private void ForEachImage(Action<string> action) => this.Benchmarks.ForEachImageParallel(action);

        private void SystemDrawingBenchmarkParallel() => this.ForEachImage(this.Benchmarks.SystemDrawingResize);

        private void ImageSharpBenchmarkParallel() => this.ForEachImage(this.Benchmarks.ImageSharpResize);

        private void MagickBenchmarkParallel() => this.ForEachImage(this.Benchmarks.MagickResize);

        private void MagicScalerBenchmarkParallel() => this.ForEachImage(this.Benchmarks.MagicScalerResize);

        private void SkiaBitmapBenchmarkParallel() => this.ForEachImage(this.Benchmarks.SkiaBitmapResize);

        private void NetVipsBenchmarkParallel() => this.ForEachImage(this.Benchmarks.NetVipsResize);
    }
}
