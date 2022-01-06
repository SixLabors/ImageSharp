// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.General
{
    public class Buffer2D_DangerousGetRowSpan
    {
        private const int Height = 1024;

        [Params(0.5, 2.0, 10.0)] public double SizeMegaBytes { get; set; }

        private Buffer2D<Rgba32> buffer;

        [GlobalSetup]
        public unsafe void Setup()
        {
            int totalElements = (int)(1024 * 1024 * this.SizeMegaBytes) / sizeof(Rgba32);

            int width = totalElements / Height;
            MemoryAllocator allocator = Configuration.Default.MemoryAllocator;
            this.buffer = allocator.Allocate2D<Rgba32>(width, Height);
        }

        [GlobalCleanup]
        public void Cleanup() => this.buffer.Dispose();

        [Benchmark]
        public int DangerousGetRowSpan() =>
            this.buffer.DangerousGetRowSpan(1).Length +
            this.buffer.DangerousGetRowSpan(Height - 1).Length;

        // BenchmarkDotNet=v0.13.0, OS=Windows 10.0.19044
        // Intel Core i9-10900X CPU 3.70GHz, 1 CPU, 20 logical and 10 physical cores
        //
        // |              Method | SizeMegaBytes |      Mean |     Error |    StdDev |
        // |-------------------- |-------------- |----------:|----------:|----------:|
        // | DangerousGetRowSpan |           0.5 |  7.498 ns | 0.1784 ns | 0.3394 ns |
        // | DangerousGetRowSpan |             2 |  6.542 ns | 0.1565 ns | 0.3659 ns |
        // | DangerousGetRowSpan |            10 | 38.556 ns | 0.6604 ns | 0.8587 ns |
    }
}
