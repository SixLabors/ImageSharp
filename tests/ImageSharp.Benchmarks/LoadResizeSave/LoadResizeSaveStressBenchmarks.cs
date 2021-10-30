// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Benchmarks.LoadResizeSave
{
    // See README.md for instructions about initialization.
    [MemoryDiagnoser]
    [ShortRunJob]
    public class LoadResizeSaveStressBenchmarks
    {
        private LoadResizeSaveStressRunner runner;

        // private const JpegKind Filter = JpegKind.Progressive;
        private const JpegKind Filter = JpegKind.Any;

#pragma warning disable CS0618 // 'ArrayPoolMemoryAllocator' is obsolete
        private ArrayPoolMemoryAllocator arrayPoolMemoryAllocator;
#pragma warning restore CS0618
        private MemoryAllocator defaultMemoryAllocator;

        [GlobalSetup]
        public void Setup()
        {
            this.runner = new LoadResizeSaveStressRunner()
            {
                ImageCount = Environment.ProcessorCount,
                Filter = Filter
            };
            Console.WriteLine($"ImageCount: {this.runner.ImageCount} Filter: {Filter}");
            this.runner.Init();
            this.defaultMemoryAllocator = Configuration.Default.MemoryAllocator;

#pragma warning disable CS0618 // 'ArrayPoolMemoryAllocator' is obsolete
            this.arrayPoolMemoryAllocator = ArrayPoolMemoryAllocator.CreateDefault();
#pragma warning restore CS0618
        }

        private void ForEachImage(Action<string> action, int maxDegreeOfParallelism)
        {
            this.runner.MaxDegreeOfParallelism = maxDegreeOfParallelism;
            this.runner.ForEachImageParallel(action);
        }

        public int[] ParallelismValues { get; } =
        {
            Environment.ProcessorCount,
            // Environment.ProcessorCount / 2,
            // Environment.ProcessorCount / 4,
            // 1
        };

        [Benchmark]
        [ArgumentsSource(nameof(ParallelismValues))]
        public void SystemDrawing(int maxDegreeOfParallelism) => this.ForEachImage(this.runner.SystemDrawingResize, maxDegreeOfParallelism);

        [Benchmark(Baseline = true)]
        [ArgumentsSource(nameof(ParallelismValues))]
        public void ImageSharp_DefaultMemoryAllocator(int maxDegreeOfParallelism)
        {
            Configuration.Default.MemoryAllocator = this.defaultMemoryAllocator;
            this.ForEachImage(this.runner.ImageSharpResize, maxDegreeOfParallelism);
        }

        [Benchmark]
        [ArgumentsSource(nameof(ParallelismValues))]
        public void ImageSharp_ArrayPoolMemoryAllocator(int maxDegreeOfParallelism)
        {
            Configuration.Default.MemoryAllocator = this.arrayPoolMemoryAllocator;
            this.ForEachImage(this.runner.ImageSharpResize, maxDegreeOfParallelism);
        }

        [Benchmark]
        [ArgumentsSource(nameof(ParallelismValues))]
        public void Magick(int maxDegreeOfParallelism) => this.ForEachImage(this.runner.MagickResize, maxDegreeOfParallelism);

        [Benchmark]
        [ArgumentsSource(nameof(ParallelismValues))]
        public void MagicScaler(int maxDegreeOfParallelism) => this.ForEachImage(this.runner.MagicScalerResize, maxDegreeOfParallelism);

        [Benchmark]
        [ArgumentsSource(nameof(ParallelismValues))]
        public void SkiaBitmap(int maxDegreeOfParallelism) => this.ForEachImage(this.runner.SkiaBitmapResize, maxDegreeOfParallelism);

        [Benchmark]
        [ArgumentsSource(nameof(ParallelismValues))]
        public void SkiaBitmapDecodeToTargetSize(int maxDegreeOfParallelism) => this.ForEachImage(this.runner.SkiaBitmapDecodeToTargetSize, maxDegreeOfParallelism);

        [Benchmark]
        [ArgumentsSource(nameof(ParallelismValues))]
        public void NetVips(int maxDegreeOfParallelism) => this.ForEachImage(this.runner.NetVipsResize, maxDegreeOfParallelism);
    }
}
