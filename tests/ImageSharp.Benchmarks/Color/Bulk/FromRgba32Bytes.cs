// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Benchmarks.ColorSpaces.Bulk
{
    public abstract class FromRgba32Bytes<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private IMemoryOwner<TPixel> destination;

        private IMemoryOwner<byte> source;

        private Configuration configuration;

        [Params(
            128,
            1024,
            2048)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.configuration = Configuration.Default;
            this.destination = this.configuration.MemoryAllocator.Allocate<TPixel>(this.Count);
            this.source = this.configuration.MemoryAllocator.Allocate<byte>(this.Count * 4);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.destination.Dispose();
            this.source.Dispose();
        }

        // [Benchmark]
        public void Naive()
        {
            Span<byte> s = this.source.GetSpan();
            Span<TPixel> d = this.destination.GetSpan();

            for (int i = 0; i < this.Count; i++)
            {
                int i4 = i * 4;
                var c = default(TPixel);
                c.FromRgba32(new Rgba32(s[i4], s[i4 + 1], s[i4 + 2], s[i4 + 3]));
                d[i] = c;
            }
        }

        [Benchmark(Baseline = true)]
        public void CommonBulk()
        {
            new PixelOperations<TPixel>().FromRgba32Bytes(this.configuration, this.source.GetSpan(), this.destination.GetSpan(), this.Count);
        }

        [Benchmark]
        public void OptimizedBulk()
        {
           PixelOperations<TPixel>.Instance.FromRgba32Bytes(this.configuration, this.source.GetSpan(), this.destination.GetSpan(), this.Count);
        }
    }

    public class FromRgba32Bytes_ToRgba32 : FromRgba32Bytes<Rgba32>
    {
    }

    public class FromRgba32Bytes_ToBgra32 : FromRgba32Bytes<Bgra32>
    {
        // RESULTS:
        //         Method | Count |       Mean |     Error |    StdDev | Scaled |
        // -------------- |------ |-----------:|----------:|----------:|-------:|
        //     CommonBulk |   128 |   207.1 ns |  3.723 ns |  3.300 ns |   1.00 |
        //  OptimizedBulk |   128 |   166.5 ns |  1.204 ns |  1.005 ns |   0.80 |
        //                |       |            |           |           |        |
        //     CommonBulk |  1024 | 1,333.9 ns | 12.426 ns | 11.624 ns |   1.00 |
        //  OptimizedBulk |  1024 |   974.1 ns | 18.803 ns | 16.669 ns |   0.73 |
        //                |       |            |           |           |        |
        //     CommonBulk |  2048 | 2,625.4 ns | 30.143 ns | 26.721 ns |   1.00 |
        //  OptimizedBulk |  2048 | 1,843.0 ns | 20.505 ns | 18.177 ns |   0.70 |
    }
}
