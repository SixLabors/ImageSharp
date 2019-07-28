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
    public abstract class FromVector4<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        protected IMemoryOwner<Vector4> source;

        protected IMemoryOwner<TPixel> destination;

        protected Configuration Configuration => Configuration.Default;

        [Params(
            64,
            2048
            )]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.destination = this.Configuration.MemoryAllocator.Allocate<TPixel>(this.Count);
            this.source = this.Configuration.MemoryAllocator.Allocate<Vector4>(this.Count);
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
                Unsafe.Add(ref d, i).FromVector4(Unsafe.Add(ref s, i));
            }
        }

        [Benchmark]
        public void PixelOperations_Base()
        {
            new PixelOperations<TPixel>().FromVector4Destructive(this.Configuration, this.source.GetSpan(), this.destination.GetSpan());
        }

        [Benchmark]
        public void PixelOperations_Specialized()
        {
            PixelOperations<TPixel>.Instance.FromVector4Destructive(this.Configuration, this.source.GetSpan(), this.destination.GetSpan());
        }
    }

    public class FromVector4Rgba32 : FromVector4<Rgba32>
    {
        [Benchmark]
        public void FallbackIntrinsics128()
        {
            Span<float> sBytes = MemoryMarshal.Cast<Vector4, float>(this.source.GetSpan());
            Span<byte> dFloats = MemoryMarshal.Cast<Rgba32, byte>(this.destination.GetSpan());

            SimdUtils.FallbackIntrinsics128.BulkConvertNormalizedFloatToByteClampOverflows(sBytes, dFloats);
        }

        [Benchmark(Baseline = true)]
        public void BasicIntrinsics256()
        {
            Span<float> sBytes = MemoryMarshal.Cast<Vector4, float>(this.source.GetSpan());
            Span<byte> dFloats = MemoryMarshal.Cast<Rgba32, byte>(this.destination.GetSpan());

            SimdUtils.BasicIntrinsics256.BulkConvertNormalizedFloatToByteClampOverflows(sBytes, dFloats);
        }

        [Benchmark]
        public void ExtendedIntrinsic()
        {
            Span<float> sBytes = MemoryMarshal.Cast<Vector4, float>(this.source.GetSpan());
            Span<byte> dFloats = MemoryMarshal.Cast<Rgba32, byte>(this.destination.GetSpan());

            SimdUtils.ExtendedIntrinsics.BulkConvertNormalizedFloatToByteClampOverflows(sBytes, dFloats);
        }

        // RESULTS (2018 October):
        //                       Method | Runtime | Count |         Mean |        Error |      StdDev | Scaled | ScaledSD |  Gen 0 | Allocated |
        // ---------------------------- |-------- |------ |-------------:|-------------:|------------:|-------:|---------:|-------:|----------:|
        //        FallbackIntrinsics128 |     Clr |    64 |    340.38 ns |    22.319 ns |   1.2611 ns |   1.41 |     0.01 |      - |       0 B |
        //           BasicIntrinsics256 |     Clr |    64 |    240.79 ns |    11.421 ns |   0.6453 ns |   1.00 |     0.00 |      - |       0 B |
        //            ExtendedIntrinsic |     Clr |    64 |    199.09 ns |   124.239 ns |   7.0198 ns |   0.83 |     0.02 |      - |       0 B |
        //         PixelOperations_Base |     Clr |    64 |    647.99 ns |    24.003 ns |   1.3562 ns |   2.69 |     0.01 | 0.0067 |      24 B |
        //  PixelOperations_Specialized |     Clr |    64 |    259.79 ns |    13.391 ns |   0.7566 ns |   1.08 |     0.00 |      - |       0 B | <--- ceremonial overhead has been minimized!
        //                              |         |       |              |              |             |        |          |        |           |
        //        FallbackIntrinsics128 |    Core |    64 |    234.64 ns |    12.320 ns |   0.6961 ns |   1.58 |     0.00 |      - |       0 B |
        //           BasicIntrinsics256 |    Core |    64 |    148.87 ns |     2.794 ns |   0.1579 ns |   1.00 |     0.00 |      - |       0 B |
        //            ExtendedIntrinsic |    Core |    64 |     94.06 ns |    10.015 ns |   0.5659 ns |   0.63 |     0.00 |      - |       0 B |
        //         PixelOperations_Base |    Core |    64 |    573.52 ns |    31.865 ns |   1.8004 ns |   3.85 |     0.01 | 0.0067 |      24 B |
        //  PixelOperations_Specialized |    Core |    64 |    117.21 ns |    13.264 ns |   0.7494 ns |   0.79 |     0.00 |      - |       0 B |
        //                              |         |       |              |              |             |        |          |        |           |
        //        FallbackIntrinsics128 |     Clr |  2048 |  6,735.93 ns | 2,139.340 ns | 120.8767 ns |   1.71 |     0.03 |      - |       0 B |
        //           BasicIntrinsics256 |     Clr |  2048 |  3,929.29 ns |   334.027 ns |  18.8731 ns |   1.00 |     0.00 |      - |       0 B |
        //            ExtendedIntrinsic |     Clr |  2048 |  2,226.01 ns |   130.525 ns |   7.3749 ns |!! 0.57 |     0.00 |      - |       0 B | <--- ExtendedIntrinsics rock!
        //         PixelOperations_Base |     Clr |  2048 | 16,760.84 ns |   367.800 ns |  20.7814 ns |   4.27 |     0.02 |      - |      24 B | <--- Extra copies using "Vector4 TPixel.ToVector4()"
        //  PixelOperations_Specialized |     Clr |  2048 |  3,986.03 ns |   237.238 ns |  13.4044 ns |   1.01 |     0.00 |      - |       0 B | <--- can't yet detect whether ExtendedIntrinsics are available :(
        //                              |         |       |              |              |             |        |          |        |           |
        //        FallbackIntrinsics128 |    Core |  2048 |  6,644.65 ns | 2,677.090 ns | 151.2605 ns |   1.69 |     0.05 |      - |       0 B |
        //           BasicIntrinsics256 |    Core |  2048 |  3,923.70 ns | 1,971.760 ns | 111.4081 ns |   1.00 |     0.00 |      - |       0 B |
        //            ExtendedIntrinsic |    Core |  2048 |  2,092.32 ns |   375.657 ns |  21.2253 ns |!! 0.53 |     0.01 |      - |       0 B | <--- ExtendedIntrinsics rock!
        //         PixelOperations_Base |    Core |  2048 | 16,875.73 ns | 1,271.957 ns |  71.8679 ns |   4.30 |     0.10 |      - |      24 B |
        //  PixelOperations_Specialized |    Core |  2048 |  2,129.92 ns |   262.888 ns |  14.8537 ns |!! 0.54 |     0.01 |      - |       0 B | <--- ExtendedIntrinsics rock!
    }
}