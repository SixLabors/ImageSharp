// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using SixLabors.ImageSharp.Benchmarks.LoadResizeSave;

namespace SixLabors.ImageSharp.Tests.ProfilingSandbox
{
    // See ImageSharp.Benchmarks/LoadResizeSave/README.md
    internal class LoadResizeSaveParallelMemoryStress
    {
        private readonly LoadResizeSaveStressRunner benchmarks;

        public LoadResizeSaveParallelMemoryStress()
        {
            this.benchmarks = new LoadResizeSaveStressRunner();
            this.benchmarks.Init();
        }

        public static void Run()
        {
            Console.WriteLine(@"Choose a library for image resizing stress test:

1. System.Drawing
2. ImageSharp
3. MagicScaler
4. SkiaSharp
5. NetVips
6. ImageMagick
7. FreeImage
");

            ConsoleKey key = Console.ReadKey().Key;
            if (key < ConsoleKey.D1 || key > ConsoleKey.D7)
            {
                Console.WriteLine("Unrecognized command.");
                return;
            }

            try
            {
                var lrs = new LoadResizeSaveParallelMemoryStress();
                Console.WriteLine("\nRunning...");
                var timer = new Stopwatch();
                timer.Start();

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
                        lrs.SkiaCanvasBenchmarkParallel();
                        break;
                    case ConsoleKey.D5:
                        lrs.NetVipsBenchmarkParallel();
                        break;
                    case ConsoleKey.D6:
                        lrs.MagickBenchmarkParallel();
                        break;
                    case ConsoleKey.D7:
                        lrs.FreeImageBenchmarkParallel();
                        break;
                }

                timer.Stop();
                Console.WriteLine($"Completed in {timer.ElapsedMilliseconds / 1000.0:f3}sec");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private void ForEachImage(Action<string> action) => Parallel.ForEach(this.benchmarks.Images, action);

        private void SystemDrawingBenchmarkParallel() => this.ForEachImage(this.benchmarks.SystemDrawingResize);

        private void ImageSharpBenchmarkParallel() => this.ForEachImage(this.benchmarks.ImageSharpResize);

        private void MagickBenchmarkParallel() => this.ForEachImage(this.benchmarks.MagickResize);

        private void FreeImageBenchmarkParallel() => this.ForEachImage(this.benchmarks.FreeImageResize);

        private void MagicScalerBenchmarkParallel() => this.ForEachImage(this.benchmarks.MagicScalerResize);

        private void SkiaCanvasBenchmarkParallel() => this.ForEachImage(this.benchmarks.SkiaCanvasResize);

        private void SkiaBitmapBenchmarkParallel() => this.ForEachImage(this.benchmarks.SkiaBitmapResize);

        private void NetVipsBenchmarkParallel() => this.ForEachImage(this.benchmarks.NetVipsResize);
    }
}
