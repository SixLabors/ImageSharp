// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Buffers;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Benchmarks.ColorSpaces.Bulk
{
    public abstract class Rgb24Bytes<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        private IMemoryOwner<TPixel> source;

        private IMemoryOwner<byte> destination;

        private Configuration configuration;

        [Params(16, 128, 1024)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.configuration = Configuration.Default;
            this.source = this.configuration.MemoryAllocator.Allocate<TPixel>(this.Count);
            this.destination = this.configuration.MemoryAllocator.Allocate<byte>(this.Count * 3);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.source.Dispose();
            this.destination.Dispose();
        }

        [Benchmark(Baseline = true)]
        public void CommonBulk() =>
            new PixelOperations<TPixel>().ToRgb24Bytes(
                this.configuration,
                this.source.GetSpan(),
                this.destination.GetSpan(),
                this.Count);

        [Benchmark]
        public void OptimizedBulk() =>
            PixelOperations<TPixel>.Instance.ToRgb24Bytes(
                this.configuration,
                this.source.GetSpan(),
                this.destination.GetSpan(),
                this.Count);
    }

    public class Rgb24Bytes_Rgba32 : Rgb24Bytes<Rgba32>
    {
    }
}
