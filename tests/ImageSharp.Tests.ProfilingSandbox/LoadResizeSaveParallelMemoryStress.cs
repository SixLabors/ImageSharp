// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Text;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using SixLabors.ImageSharp.Benchmarks.LoadResizeSave;

namespace SixLabors.ImageSharp.Tests.ProfilingSandbox
{
    // See ImageSharp.Benchmarks/LoadResizeSave/README.md
    internal class LoadResizeSaveParallelMemoryStress
    {
        private readonly LoadResizeSaveStressRunner benchmarks;

        private LoadResizeSaveParallelMemoryStress()
        {
            this.benchmarks = new LoadResizeSaveStressRunner();
            this.benchmarks.Init();
        }

        private double TotalProcessedMegapixels => this.benchmarks.TotalProcessedMegapixels;

        public static void Run()
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
                return;
            }

            try
            {
                var lrs = new LoadResizeSaveParallelMemoryStress();
                lrs.benchmarks.MaxDegreeOfParallelism = 10;

                Console.WriteLine($"\nEnvironment.ProcessorCount={Environment.ProcessorCount}");
                Console.WriteLine($"Running with MaxDegreeOfParallelism={lrs.benchmarks.MaxDegreeOfParallelism} ...");
                var timer = Stopwatch.StartNew();

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
                var stats = new Stats(timer, lrs.TotalProcessedMegapixels);
                Console.WriteLine("Done. TotalProcessedMegapixels: " + lrs.TotalProcessedMegapixels);
                Console.WriteLine(stats.GetMarkdown());
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
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

        private void ForEachImage(Action<string> action) => this.benchmarks.ForEachImageParallel(action);

        private void SystemDrawingBenchmarkParallel() => this.ForEachImage(this.benchmarks.SystemDrawingResize);

        private void ImageSharpBenchmarkParallel() => this.ForEachImage(this.benchmarks.ImageSharpResize);

        private void MagickBenchmarkParallel() => this.ForEachImage(this.benchmarks.MagickResize);

        private void MagicScalerBenchmarkParallel() => this.ForEachImage(this.benchmarks.MagicScalerResize);

        private void SkiaBitmapBenchmarkParallel() => this.ForEachImage(this.benchmarks.SkiaBitmapResize);

        private void NetVipsBenchmarkParallel() => this.ForEachImage(this.benchmarks.NetVipsResize);
    }
}
