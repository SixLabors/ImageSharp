// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.General
{
    public class Buffer2D_DangerousGetRowSpan
    {
        [Params(true, false)]
        public bool IsDiscontiguousBuffer { get; set; }

        private Buffer2D<Rgba32> buffer;

        [GlobalSetup]
        public void Setup()
        {
            MemoryAllocator allocator = Configuration.Default.MemoryAllocator;
            this.buffer = this.IsDiscontiguousBuffer
                ? allocator.Allocate2D<Rgba32>(4000, 1000)
                : allocator.Allocate2D<Rgba32>(500, 1000);
        }

        [GlobalCleanup]
        public void Cleanup() => this.buffer.Dispose();

        [Benchmark]
        public int DangerousGetRowSpan() =>
            this.buffer.DangerousGetRowSpan(1).Length +
            this.buffer.DangerousGetRowSpan(999).Length;

        // BenchmarkDotNet=v0.13.0, OS=Windows 10.0.19044
        // Intel Core i9-10900X CPU 3.70GHz, 1 CPU, 20 logical and 10 physical cores
        //
        // |              Method | IsDiscontiguousBuffer |     Mean |    Error |   StdDev |
        // |-------------------- |---------------------- |---------:|---------:|---------:|
        // | DangerousGetRowSpan |                 False | 74.96 ns | 1.505 ns | 1.478 ns |
        // | DangerousGetRowSpan |                  True | 71.49 ns | 1.446 ns | 2.120 ns |
    }
}
