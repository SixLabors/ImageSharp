// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.LoadResizeSave
{
    public class LoadResizeSaveStress_NonParallel
    {
        private LoadResizeSaveStressRunner benchmarks;

        [GlobalSetup]
        public void Setup()
        {
            this.benchmarks = new LoadResizeSaveStressRunner() { ImageCount = 20 };
            this.benchmarks.Init();
        }

        private void ForEachImage(Action<string> action)
        {
            foreach (string image in this.benchmarks.Images)
            {
                action(image);
            }
        }

        [Benchmark(Baseline = true, Description = "System.Drawing Load, Resize, Save")]
        public void SystemDrawingBenchmark() => this.ForEachImage(this.benchmarks.SystemDrawingResize);

        [Benchmark(Description = "ImageSharp Load, Resize, Save")]
        public void ImageSharpBenchmark() => this.ForEachImage(this.benchmarks.ImageSharpResize);

        [Benchmark(Description = "ImageMagick Load, Resize, Save")]
        public void MagickBenchmark() => this.ForEachImage(this.benchmarks.MagickResize);

        [Benchmark(Description = "ImageFree Load, Resize, Save")]
        public void FreeImageBenchmark() => this.ForEachImage(this.benchmarks.FreeImageResize);

        [Benchmark(Description = "MagicScaler Load, Resize, Save")]
        public void MagicScalerBenchmark() => this.ForEachImage(this.benchmarks.MagicScalerResize);

        [Benchmark(Description = "SkiaSharp Canvas Load, Resize, Save")]
        public void SkiaCanvasBenchmark() => this.ForEachImage(this.benchmarks.SkiaCanvasResize);

        [Benchmark(Description = "SkiaSharp Bitmap Load, Resize, Save")]
        public void SkiaBitmapBenchmark() => this.ForEachImage(this.benchmarks.SkiaBitmapResize);

        [Benchmark(Description = "NetVips Load, Resize, Save")]
        public void NetVipsBenchmark() => this.ForEachImage(this.benchmarks.NetVipsResize);
    }
}
