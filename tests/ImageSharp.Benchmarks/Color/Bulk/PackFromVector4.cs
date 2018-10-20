// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InconsistentNaming

using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.ColorSpaces.Bulk
{
    [Config(typeof(Config.ShortClr))]
    public abstract class PackFromVector4<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        protected IMemoryOwner<Vector4> source;

        protected IMemoryOwner<TPixel> destination;

        [Params(
            //64,
            2048
            )]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.destination = Configuration.Default.MemoryAllocator.Allocate<TPixel>(this.Count);
            this.source = Configuration.Default.MemoryAllocator.Allocate<Vector4>(this.Count);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.destination.Dispose();
            this.source.Dispose();
        }

        //[Benchmark]
        public void PerElement()
        {
            ref Vector4 s = ref MemoryMarshal.GetReference(this.source.GetSpan());
            ref TPixel d = ref MemoryMarshal.GetReference(this.destination.GetSpan());
            
            for (int i = 0; i < this.Count; i++)
            {
                Unsafe.Add(ref d, i).PackFromVector4(Unsafe.Add(ref s, i));
            }
        }

        [Benchmark]
        public void PixelOperations_Base()
        {
            new PixelOperations<TPixel>().PackFromVector4(this.source.GetSpan(), this.destination.GetSpan(), this.Count);
        }

        [Benchmark]
        public void PixelOperations_Specialized()
        {
            PixelOperations<TPixel>.Instance.PackFromVector4(this.source.GetSpan(), this.destination.GetSpan(), this.Count);
        }
    }

    public class PackFromVector4_Rgba32 : PackFromVector4<Rgba32>
    {
        [Benchmark]
        public void FastDefault()
        {
            ref Vector4 sBase = ref this.source.GetSpan()[0];
            ref Rgba32 dBase = ref this.destination.GetSpan()[0];

            Vector4 maxBytes = new Vector4(255);
            Vector4 half = new Vector4(0.5f);

            for (int i = 0; i < this.Count; i++)
            {
                Vector4 v = Unsafe.Add(ref sBase, i);
                v *= maxBytes;
                v += half;
                v = Vector4.Clamp(v, Vector4.Zero, maxBytes);
                ref Rgba32 d = ref Unsafe.Add(ref dBase, i);
                d.R = (byte)v.X;
                d.G = (byte)v.Y;
                d.B = (byte)v.Z;
                d.A = (byte)v.W;
            }
        }

        [Benchmark(Baseline = true)]
        public void BulkConvertNormalizedFloatToByteClampOverflows()
        {
            Span<float> sBytes = MemoryMarshal.Cast<Vector4, float>(this.source.GetSpan());
            Span<byte> dFloats = MemoryMarshal.Cast<Rgba32, byte>(this.destination.GetSpan());

            SimdUtils.BulkConvertNormalizedFloatToByteClampOverflows(sBytes, dFloats);
        }

        [Benchmark]
        public void ExtendedIntrinsic_BulkConvertNormalizedFloatToByteClampOverflows()
        {
            Span<float> sBytes = MemoryMarshal.Cast<Vector4, float>(this.source.GetSpan());
            Span<byte> dFloats = MemoryMarshal.Cast<Rgba32, byte>(this.destination.GetSpan());

            SimdUtils.ExtendedIntrinsics.BulkConvertNormalizedFloatToByteClampOverflows(sBytes, dFloats);
        }

        // RESULTS:
        //                                                            Method | Runtime | Count |      Mean |     Error |    StdDev | Scaled | ScaledSD | Allocated |
        // ----------------------------------------------------------------- |-------- |------ |----------:|----------:|----------:|-------:|---------:|----------:|
        //                                                       FastDefault |     Clr |  2048 | 15.989 us | 6.1384 us | 0.3468 us |   4.07 |     0.08 |       0 B |
        //                    BulkConvertNormalizedFloatToByteClampOverflows |     Clr |  2048 |  3.931 us | 0.6264 us | 0.0354 us |   1.00 |     0.00 |       0 B |
        //  ExtendedIntrinsic_BulkConvertNormalizedFloatToByteClampOverflows |     Clr |  2048 |  2.100 us | 0.4717 us | 0.0267 us |   0.53 |     0.01 |       0 B |
        // 
        //                                                                   |         |       |           |           |           |        |          |           |
        //                                                       FastDefault |    Core |  2048 | 14.693 us | 0.5131 us | 0.0290 us |   3.76 |     0.03 |       0 B |
        //                    BulkConvertNormalizedFloatToByteClampOverflows |    Core |  2048 |  3.913 us | 0.5661 us | 0.0320 us |   1.00 |     0.00 |       0 B |
        //  ExtendedIntrinsic_BulkConvertNormalizedFloatToByteClampOverflows |    Core |  2048 |  1.966 us | 0.4056 us | 0.0229 us |   0.50 |     0.01 |       0 B |
    }
}