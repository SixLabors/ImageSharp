// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InconsistentNaming

using System.Buffers;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Attributes.Jobs;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.ColorSpaces.Bulk
{
    public abstract class ToVector4<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        protected IMemoryOwner<TPixel> source;

        protected IMemoryOwner<Vector4> destination;

        [Params(
            //64, 
            2048)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.source = Configuration.Default.MemoryAllocator.Allocate<TPixel>(this.Count);
            this.destination = Configuration.Default.MemoryAllocator.Allocate<Vector4>(this.Count);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.source.Dispose();
            this.destination.Dispose();
        }

        [Benchmark]
        public void PerElement()
        {
            Span<TPixel> s = this.source.GetSpan();
            Span<Vector4> d = this.destination.GetSpan();

            for (int i = 0; i < this.Count; i++)
            {
                TPixel c = s[i];
                d[i] = c.ToVector4();
            }
        }

        [Benchmark(Baseline = true)]
        public void CommonBulk()
        {
            new PixelOperations<TPixel>().ToVector4(this.source.GetSpan(), this.destination.GetSpan(), this.Count);
        }

        [Benchmark]
        public void OptimizedBulk()
        {
            PixelOperations<TPixel>.Instance.ToVector4(this.source.GetSpan(), this.destination.GetSpan(), this.Count);
        }
    }

    [CoreJob]
    [ClrJob]
    public class ToVector4_Rgba32 : ToVector4<Rgba32>
    {
        class Config : ManualConfig
        {
        }

        //[Benchmark]
        public void BulkConvertByteToNormalizedFloat()
        {
            Span<byte> sBytes = MemoryMarshal.Cast<Rgba32, byte>(this.source.GetSpan());
            Span<float> dFloats = MemoryMarshal.Cast<Vector4, float>(this.destination.GetSpan());

            SimdUtils.BulkConvertByteToNormalizedFloat(sBytes, dFloats);
        }

        [Benchmark]
        public void BulkConvertByteToNormalizedFloatFast()
        {
            Span<byte> sBytes = MemoryMarshal.Cast<Rgba32, byte>(this.source.GetSpan());
            Span<float> dFloats = MemoryMarshal.Cast<Vector4, float>(this.destination.GetSpan());

            SimdUtils.BulkConvertByteToNormalizedFloatFast(sBytes, dFloats);
        }

    }
}