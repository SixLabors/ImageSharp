// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Numerics;
using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Benchmarks.ColorSpaces.Bulk
{
    public abstract class ToVector4<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        protected IMemoryOwner<TPixel> source;

        protected IMemoryOwner<Vector4> destination;

        protected Configuration Configuration => Configuration.Default;

        [Params(64, 256, 2048)] // 512, 1024
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.source = this.Configuration.MemoryAllocator.Allocate<TPixel>(this.Count);
            this.destination = this.Configuration.MemoryAllocator.Allocate<Vector4>(this.Count);
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
            Span<Vector4> d = this.destination.GetSpan();

            for (int i = 0; i < this.Count; i++)
            {
                d[i] = s[i].ToVector4();
            }
        }

        [Benchmark]
        public void PixelOperations_Specialized()
        {
            PixelOperations<TPixel>.Instance.ToVector4(
                this.Configuration,
                this.source.GetSpan(),
                this.destination.GetSpan());
        }
    }
}
