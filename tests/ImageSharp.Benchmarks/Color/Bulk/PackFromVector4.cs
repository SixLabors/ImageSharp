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
            2048)]
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

        [Benchmark]
        public void PerElement()
        {
            ref Vector4 s = ref MemoryMarshal.GetReference(this.source.GetSpan());
            ref TPixel d = ref MemoryMarshal.GetReference(this.destination.GetSpan());
            
            for (int i = 0; i < this.Count; i++)
            {
                Unsafe.Add(ref d, i).PackFromVector4(Unsafe.Add(ref s, i));
            }
        }

        [Benchmark(Baseline = true)]
        public void CommonBulk()
        {
            new PixelOperations<TPixel>().PackFromVector4(this.source.GetSpan(), this.destination.GetSpan(), this.Count);
        }

        [Benchmark]
        public void OptimizedBulk()
        {
            PixelOperations<TPixel>.Instance.PackFromVector4(this.source.GetSpan(), this.destination.GetSpan(), this.Count);
        }
    }

    public class PackFromVector4_Rgba32 : PackFromVector4<Rgba32>
    {
        //[Benchmark]
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

        // TODO: Check again later!
        // RESULTS:
        //
        // BenchmarkDotNet=v0.10.14, OS=Windows 10.0.17134
        // Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
        // Frequency=2742187 Hz, Resolution=364.6724 ns, Timer=TSC
        // .NET Core SDK=2.1.400-preview-009063
        //   [Host]     : .NET Core 2.1.1 (CoreCLR 4.6.26606.02, CoreFX 4.6.26606.05), 64bit RyuJIT
        //   Job-XIFINS : .NET Framework 4.7.1 (CLR 4.0.30319.42000), 64bit RyuJIT-v4.7.3190.0
        //   Job-RTQZPN : .NET Core 2.1.1 (CoreCLR 4.6.26606.02, CoreFX 4.6.26606.05), 64bit RyuJIT
        // 
        // LaunchCount=1  TargetCount=3  WarmupCount=3
        // 
        //                                                            Method | Runtime | Count |      Mean |      Error |    StdDev | Scaled | ScaledSD | Allocated |
        // ----------------------------------------------------------------- |-------- |------ |----------:|-----------:|----------:|-------:|---------:|----------:|
        //  ExtendedIntrinsic_BulkConvertNormalizedFloatToByteClampOverflows |     Clr |  2048 |  3.755 us |  0.8959 us | 0.0506 us |   0.22 |     0.00 |       0 B |
        //                                                        PerElement |     Clr |  2048 | 17.387 us | 15.1569 us | 0.8564 us |   1.02 |     0.04 |       0 B |
        //                                                        CommonBulk |     Clr |  2048 | 17.121 us |  0.7634 us | 0.0431 us |   1.00 |     0.00 |      24 B |
        //                                                     OptimizedBulk |     Clr |  2048 |  4.018 us |  0.3858 us | 0.0218 us |   0.23 |     0.00 |       0 B |
        //                                                                   |         |       |           |            |           |        |          |           |
        //  ExtendedIntrinsic_BulkConvertNormalizedFloatToByteClampOverflows |    Core |  2048 | 22.232 us |  1.6154 us | 0.0913 us |   1.31 |     0.04 |       0 B |
        //                                                        PerElement |    Core |  2048 | 16.741 us |  2.9254 us | 0.1653 us |   0.98 |     0.03 |       0 B |
        //                                                        CommonBulk |    Core |  2048 | 17.022 us | 11.4894 us | 0.6492 us |   1.00 |     0.00 |      24 B |
        //                                                     OptimizedBulk |    Core |  2048 |  3.707 us |  0.1500 us | 0.0085 us |   0.22 |     0.01 |       0 B |
    }
}