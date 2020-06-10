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
    public abstract class ToRgba32Bytes<TPixel>
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
            this.destination = this.configuration.MemoryAllocator.Allocate<byte>(this.Count * 4);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.source.Dispose();
            this.destination.Dispose();
        }

        // [Benchmark]
        public void Naive()
        {
            Span<TPixel> s = this.source.GetSpan();
            Span<byte> d = this.destination.GetSpan();

            for (int i = 0; i < this.Count; i++)
            {
                TPixel c = s[i];
                int i4 = i * 4;
                Rgba32 rgba = default;
                c.ToRgba32(ref rgba);
                d[i4] = rgba.R;
                d[i4 + 1] = rgba.G;
                d[i4 + 2] = rgba.B;
                d[i4 + 3] = rgba.A;
            }
        }

        [Benchmark(Baseline = true)]
        public void CommonBulk() =>
            new PixelOperations<TPixel>().ToRgba32Bytes(
                this.configuration,
                this.source.GetSpan(),
                this.destination.GetSpan(),
                this.Count);

        [Benchmark]
        public void OptimizedBulk() =>
            PixelOperations<TPixel>.Instance.ToRgba32Bytes(
                this.configuration,
                this.source.GetSpan(),
                this.destination.GetSpan(),
                this.Count);
    }

    public class ToRgba32Bytes_FromRgba32 : ToRgba32Bytes<Rgba32>
    {
    }

    public class ToRgba32Bytes_FromArgb32 : ToRgba32Bytes<Argb32>
    {
    }

    public class ToRgba32Bytes_FromBgra32 : ToRgba32Bytes<Bgra32>
    {
    }
}
