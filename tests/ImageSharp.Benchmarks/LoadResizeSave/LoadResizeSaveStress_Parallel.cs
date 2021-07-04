// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.LoadResizeSave
{
    [MemoryDiagnoser]
    public class LoadResizeSaveStress_Parallel
    {
        private LoadResizeSaveStressRunner benchmarks;

        [GlobalSetup]
        public void Setup()
        {
            this.benchmarks = new LoadResizeSaveStressRunner() { ImageCount = 20 };
            this.benchmarks.Init();
        }

        private void ForEachImage(Action<string> action) => Parallel.ForEach(this.benchmarks.Images, action);

        [Benchmark(Baseline = true, Description = "System.Drawing Load, Resize, Save - Parallel")]
        public void SystemDrawingBenchmarkParallel() => this.ForEachImage(this.benchmarks.SystemDrawingResize);

        [Benchmark(Description = "ImageSharp Load, Resize, Save - Parallel")]
        public void ImageSharpBenchmarkParallel() => this.ForEachImage(this.benchmarks.ImageSharpResize);

        [Benchmark(Description = "ImageMagick Load, Resize, Save - Parallel")]
        public void MagickBenchmarkParallel() => this.ForEachImage(this.benchmarks.MagickResize);

        [Benchmark(Description = "ImageFree Load, Resize, Save - Parallel")]
        public void FreeImageBenchmarkParallel() => this.ForEachImage(this.benchmarks.FreeImageResize);

        [Benchmark(Description = "MagicScaler Load, Resize, Save - Parallel")]
        public void MagicScalerBenchmarkParallel() => this.ForEachImage(this.benchmarks.MagicScalerResize);

        [Benchmark(Description = "SkiaSharp Canvas Load, Resize, Save - Parallel")]
        public void SkiaCanvasBenchmarkParallel() => this.ForEachImage(this.benchmarks.SkiaCanvasResize);

        [Benchmark(Description = "SkiaSharp Bitmap Load, Resize, Save - Parallel")]
        public void SkiaBitmapBenchmarkParallel() => this.ForEachImage(this.benchmarks.SkiaBitmapResize);

        [Benchmark(Description = "NetVips Load, Resize, Save - Parallel")]
        public void NetVipsBenchmarkParallel() => this.ForEachImage(this.benchmarks.NetVipsResize);
    }
}
