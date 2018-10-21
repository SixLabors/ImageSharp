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
            64,
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
        //                                                             Method | Runtime | Count |         Mean |        Error |     StdDev | Scaled | ScaledSD |  Gen 0 | Allocated |
        // ------------------------------------------------------------------ |-------- |------ |-------------:|-------------:|-----------:|-------:|---------:|-------:|----------:|
        //                                                          BasicBulk |     Clr |    64 |    581.62 ns |    33.625 ns |  1.8999 ns |   2.27 |     0.02 |      - |       0 B |
        //  BasicIntrinsics256_BulkConvertNormalizedFloatToByteClampOverflows |     Clr |    64 |    256.66 ns |    45.153 ns |  2.5512 ns |   1.00 |     0.00 |      - |       0 B |
        //   ExtendedIntrinsic_BulkConvertNormalizedFloatToByteClampOverflows |     Clr |    64 |    201.92 ns |    30.161 ns |  1.7042 ns |   0.79 |     0.01 |      - |       0 B |
        //                                               PixelOperations_Base |     Clr |    64 |    665.01 ns |    13.032 ns |  0.7363 ns |   2.59 |     0.02 | 0.0067 |      24 B |
        //                                        PixelOperations_Specialized |     Clr |    64 |    295.14 ns |    26.335 ns |  1.4880 ns |   1.15 |     0.01 |      - |       0 B |
        //                                                                    |         |       |              |              |            |        |          |        |           |
        //                                                          BasicBulk |    Core |    64 |    513.22 ns |    91.110 ns |  5.1479 ns |   3.19 |     0.03 |      - |       0 B |
        //  BasicIntrinsics256_BulkConvertNormalizedFloatToByteClampOverflows |    Core |    64 |    160.76 ns |     2.760 ns |  0.1559 ns |   1.00 |     0.00 |      - |       0 B |
        //   ExtendedIntrinsic_BulkConvertNormalizedFloatToByteClampOverflows |    Core |    64 |     95.98 ns |    10.077 ns |  0.5694 ns |   0.60 |     0.00 |      - |       0 B |
        //                                               PixelOperations_Base |    Core |    64 |    591.74 ns |    49.856 ns |  2.8170 ns |   3.68 |     0.01 | 0.0067 |      24 B |
        //                                        PixelOperations_Specialized |    Core |    64 |    149.11 ns |     4.485 ns |  0.2534 ns |   0.93 |     0.00 |      - |       0 B |
        //                                                                    |         |       |              |              |            |        |          |        |           |
        //                                                          BasicBulk |     Clr |  2048 | 15,345.85 ns | 1,213.551 ns | 68.5679 ns |   3.90 |     0.01 |      - |       0 B |
        //  BasicIntrinsics256_BulkConvertNormalizedFloatToByteClampOverflows |     Clr |  2048 |  3,939.49 ns |    71.101 ns |  4.0173 ns |   1.00 |     0.00 |      - |       0 B |
        //   ExtendedIntrinsic_BulkConvertNormalizedFloatToByteClampOverflows |     Clr |  2048 |  2,272.61 ns |   110.671 ns |  6.2531 ns |   0.58 |     0.00 |      - |       0 B |
        //                                               PixelOperations_Base |     Clr |  2048 | 17,422.47 ns |   811.733 ns | 45.8644 ns |   4.42 |     0.01 |      - |      24 B |
        //                                        PixelOperations_Specialized |     Clr |  2048 |  3,984.26 ns |   110.352 ns |  6.2351 ns |   1.01 |     0.00 |      - |       0 B |
        //                                                                    |         |       |              |              |            |        |          |        |           |
        //                                                          BasicBulk |    Core |  2048 | 14,950.43 ns |   699.309 ns | 39.5123 ns |   3.76 |     0.02 |      - |       0 B |
        //  BasicIntrinsics256_BulkConvertNormalizedFloatToByteClampOverflows |    Core |  2048 |  3,978.28 ns |   481.105 ns | 27.1833 ns |   1.00 |     0.00 |      - |       0 B |
        //   ExtendedIntrinsic_BulkConvertNormalizedFloatToByteClampOverflows |    Core |  2048 |  2,169.54 ns |    75.606 ns |  4.2719 ns | !!0.55!|     0.00 |      - |       0 B |
        //                                               PixelOperations_Base |    Core |  2048 | 18,403.62 ns | 1,494.056 ns | 84.4169 ns |   4.63 |     0.03 |      - |      24 B |
        //                                        PixelOperations_Specialized |    Core |  2048 |  2,227.60 ns |   486.761 ns | 27.5029 ns | !!0.56!|     0.01 |      - |       0 B |
    }
}