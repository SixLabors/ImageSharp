// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InconsistentNaming

using System.Buffers;
using System;

using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.ColorSpaces.Bulk
{
    public abstract class ToXyz<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private IMemoryOwner<TPixel> source;

        private IMemoryOwner<byte> destination;

        [Params(16, 128, 1024)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.source = Configuration.Default.MemoryAllocator.Allocate<TPixel>(this.Count);
            this.destination = Configuration.Default.MemoryAllocator.Allocate<byte>(this.Count * 3);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.source.Dispose();
            this.destination.Dispose();
        }

        [Benchmark(Baseline = true)]
        public void PerElement()
        {
            Span<TPixel> s = this.source.GetSpan();
            Span<byte> d = this.destination.GetSpan();

            var rgb = default(Rgb24);

            for (int i = 0; i < this.Count; i++)
            {
                TPixel c = s[i];
                int i3 = i * 3;
                c.ToRgb24(ref rgb);
                d[i3] = rgb.R;
                d[i3 + 1] = rgb.G;
                d[i3 + 2] = rgb.B;
            }
        }

        [Benchmark]
        public void CommonBulk()
        {
            new PixelOperations<TPixel>().ToRgb24Bytes(this.source.GetSpan(), this.destination.GetSpan(), this.Count);
        }

        [Benchmark]
        public void OptimizedBulk()
        {
            PixelOperations<TPixel>.Instance.ToRgb24Bytes(this.source.GetSpan(), this.destination.GetSpan(), this.Count);
        }
    }

    public class ToXyz_Rgba32 : ToXyz<Rgba32>
    {
    }
}