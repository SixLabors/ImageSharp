// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InconsistentNaming

using System.Buffers;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
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
            64, 
            //256,
            //512,
            //1024,
            2048
            )]
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

        //[Benchmark]
        public void PerElement()
        {
            Span<TPixel> s = this.source.GetSpan();
            Span<Vector4> d = this.destination.GetSpan();

            for (int i = 0; i < this.Count; i++)
            {
                d[i] = s[i].ToVector4();
            }
        }

        [Benchmark]
        public void PixelOperations_Base()
        {
            new PixelOperations<TPixel>().ToVector4(this.source.GetSpan(), this.destination.GetSpan(), this.Count);
        }

        [Benchmark]
        public void PixelOperations_Specialized()
        {
            PixelOperations<TPixel>.Instance.ToVector4(this.source.GetSpan(), this.destination.GetSpan(), this.Count);
        }
    }

    [Config(typeof(Config.ShortClr))]
    public class ToVector4_Rgba32 : ToVector4<Rgba32>
    {
        [Benchmark]
        public void BasicBulk()
        {
            ref Rgba32 sBase = ref this.source.GetSpan()[0];
            ref Vector4 dBase = ref this.destination.GetSpan()[0];

            Vector4 scale = new Vector4(1f / 255f);

            Vector4 v = default;

            for (int i = 0; i < this.Count; i++)
            {
                ref Rgba32 s = ref Unsafe.Add(ref sBase, i);
                v.X = s.R;
                v.Y = s.G;
                v.Z = s.B;
                v.W = s.A;
                v *= scale;
                Unsafe.Add(ref dBase, i) = v;
            }
        }
        
        [Benchmark(Baseline = true)]
        public void BasicIntrinsics256_BulkConvertByteToNormalizedFloat()
        {
            Span<byte> sBytes = MemoryMarshal.Cast<Rgba32, byte>(this.source.GetSpan());
            Span<float> dFloats = MemoryMarshal.Cast<Vector4, float>(this.destination.GetSpan());

            SimdUtils.BasicIntrinsics256.BulkConvertByteToNormalizedFloat(sBytes, dFloats);
        }

        [Benchmark]
        public void ExtendedIntrinsics_BulkConvertByteToNormalizedFloat()
        {
            Span<byte> sBytes = MemoryMarshal.Cast<Rgba32, byte>(this.source.GetSpan());
            Span<float> dFloats = MemoryMarshal.Cast<Vector4, float>(this.destination.GetSpan());

            SimdUtils.ExtendedIntrinsics.BulkConvertByteToNormalizedFloat(sBytes, dFloats);
        }

        //[Benchmark]
        public void ExtendedIntrinsics_BulkConvertByteToNormalizedFloat_2Loops()
        {
            Span<byte> sBytes = MemoryMarshal.Cast<Rgba32, byte>(this.source.GetSpan());
            Span<float> dFloats = MemoryMarshal.Cast<Vector4, float>(this.destination.GetSpan());

            int n = dFloats.Length / Vector<byte>.Count;

            ref Vector<byte> sourceBase = ref Unsafe.As<byte, Vector<byte>>(ref MemoryMarshal.GetReference((ReadOnlySpan<byte>)sBytes));
            ref Vector<float> destBase = ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(dFloats));
            ref Vector<uint> destBaseU = ref Unsafe.As<Vector<float>, Vector<uint>>(ref destBase);

            for (int i = 0; i < n; i++)
            {
                Vector<byte> b = Unsafe.Add(ref sourceBase, i);

                Vector.Widen(b, out Vector<ushort> s0, out Vector<ushort> s1);
                Vector.Widen(s0, out Vector<uint> w0, out Vector<uint> w1);
                Vector.Widen(s1, out Vector<uint> w2, out Vector<uint> w3);

                ref Vector<uint> d = ref Unsafe.Add(ref destBaseU, i * 4);
                d = w0;
                Unsafe.Add(ref d, 1) = w1;
                Unsafe.Add(ref d, 2) = w2;
                Unsafe.Add(ref d, 3) = w3;
            }

            n = dFloats.Length / Vector<float>.Count;
            var scale = new Vector<float>(1f / 255f);

            for (int i = 0; i < n; i++)
            {
                ref Vector<float> dRef = ref Unsafe.Add(ref destBase, i);

                Vector<int> du = Vector.AsVectorInt32(dRef);
                Vector<float> v = Vector.ConvertToSingle(du);
                v *= scale;

                dRef = v;
            }
        }

        //[Benchmark]
        public void ExtendedIntrinsics_BulkConvertByteToNormalizedFloat_ConvertInSameLoop()
        {
            Span<byte> sBytes = MemoryMarshal.Cast<Rgba32, byte>(this.source.GetSpan());
            Span<float> dFloats = MemoryMarshal.Cast<Vector4, float>(this.destination.GetSpan());

            int n = dFloats.Length / Vector<byte>.Count;

            ref Vector<byte> sourceBase = ref Unsafe.As<byte, Vector<byte>>(ref MemoryMarshal.GetReference((ReadOnlySpan<byte>)sBytes));
            ref Vector<float> destBase = ref Unsafe.As<float, Vector<float>>(ref MemoryMarshal.GetReference(dFloats));
            var scale = new Vector<float>(1f / 255f);

            for (int i = 0; i < n; i++)
            {
                Vector<byte> b = Unsafe.Add(ref sourceBase, i);

                Vector.Widen(b, out Vector<ushort> s0, out Vector<ushort> s1);
                Vector.Widen(s0, out Vector<uint> w0, out Vector<uint> w1);
                Vector.Widen(s1, out Vector<uint> w2, out Vector<uint> w3);

                Vector<float> f0 = ConvertToNormalizedSingle(w0, scale);
                Vector<float> f1 = ConvertToNormalizedSingle(w1, scale);
                Vector<float> f2 = ConvertToNormalizedSingle(w2, scale);
                Vector<float> f3 = ConvertToNormalizedSingle(w3, scale);

                ref Vector<float> d = ref Unsafe.Add(ref destBase, i * 4);
                d = f0;
                Unsafe.Add(ref d, 1) = f1;
                Unsafe.Add(ref d, 2) = f2;
                Unsafe.Add(ref d, 3) = f3;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector<float> ConvertToNormalizedSingle(Vector<uint> u, Vector<float> scale)
        {
            Vector<int> vi = Vector.AsVectorInt32(u);
            Vector<float> v = Vector.ConvertToSingle(vi);
            v *= scale;
            return v;
        }

        // RESULTS (2018 October):
        //
        //                                               Method | Runtime | Count |        Mean |        Error |     StdDev | Scaled | ScaledSD |  Gen 0 | Allocated |
        // ---------------------------------------------------- |-------- |------ |------------:|-------------:|-----------:|-------:|---------:|-------:|----------:|
        //                                            BasicBulk |     Clr |    64 |   267.40 ns |    30.711 ns |  1.7352 ns |   1.07 |     0.01 |      - |       0 B |
        //  BasicIntrinsics256_BulkConvertByteToNormalizedFloat |     Clr |    64 |   249.97 ns |    33.838 ns |  1.9119 ns |   1.00 |     0.00 |      - |       0 B |
        //  ExtendedIntrinsics_BulkConvertByteToNormalizedFloat |     Clr |    64 |   176.97 ns |     5.221 ns |  0.2950 ns |   0.71 |     0.00 |      - |       0 B |
        //                                 PixelOperations_Base |     Clr |    64 |   349.70 ns |   104.331 ns |  5.8949 ns |   1.40 |     0.02 | 0.0072 |      24 B |
        //                          PixelOperations_Specialized |     Clr |    64 |   288.31 ns |    26.833 ns |  1.5161 ns |   1.15 |     0.01 |      - |       0 B |
        //                                                      |         |       |             |              |            |        |          |        |           |
        //                                            BasicBulk |    Core |    64 |   185.36 ns |    30.051 ns |  1.6979 ns |   1.26 |     0.01 |      - |       0 B |
        //  BasicIntrinsics256_BulkConvertByteToNormalizedFloat |    Core |    64 |   146.84 ns |    12.674 ns |  0.7161 ns |   1.00 |     0.00 |      - |       0 B |
        //  ExtendedIntrinsics_BulkConvertByteToNormalizedFloat |    Core |    64 |    67.31 ns |     2.542 ns |  0.1436 ns |   0.46 |     0.00 |      - |       0 B |
        //                                 PixelOperations_Base |    Core |    64 |   272.03 ns |    94.419 ns |  5.3348 ns |   1.85 |     0.03 | 0.0072 |      24 B |
        //                          PixelOperations_Specialized |    Core |    64 |   121.91 ns |    31.477 ns |  1.7785 ns |   0.83 |     0.01 |      - |       0 B |
        //                                                      |         |       |             |              |            |        |          |        |           |
        //                                            BasicBulk |     Clr |  2048 | 5,133.04 ns |   284.052 ns | 16.0494 ns |   1.21 |     0.01 |      - |       0 B |
        //  BasicIntrinsics256_BulkConvertByteToNormalizedFloat |     Clr |  2048 | 4,248.58 ns | 1,095.887 ns | 61.9196 ns |   1.00 |     0.00 |      - |       0 B |
        //  ExtendedIntrinsics_BulkConvertByteToNormalizedFloat |     Clr |  2048 | 1,214.02 ns |   184.349 ns | 10.4160 ns |   0.29 |     0.00 |      - |       0 B |
        //                                 PixelOperations_Base |     Clr |  2048 | 7,096.04 ns |   362.350 ns | 20.4734 ns |   1.67 |     0.02 |      - |      24 B |
        //                          PixelOperations_Specialized |     Clr |  2048 | 4,314.19 ns |   204.964 ns | 11.5809 ns |   1.02 |     0.01 |      - |       0 B |
        //                                                      |         |       |             |              |            |        |          |        |           |
        //                                            BasicBulk |    Core |  2048 | 5,038.38 ns |   223.282 ns | 12.6158 ns |   1.20 |     0.01 |      - |       0 B |
        //  BasicIntrinsics256_BulkConvertByteToNormalizedFloat |    Core |  2048 | 4,199.17 ns |   897.985 ns | 50.7378 ns |   1.00 |     0.00 |      - |       0 B |
        //  ExtendedIntrinsics_BulkConvertByteToNormalizedFloat |    Core |  2048 | 1,113.86 ns |    64.799 ns |  3.6613 ns | !!0.27!|     0.00 |      - |       0 B |
        //                                 PixelOperations_Base |    Core |  2048 | 7,015.00 ns |   920.083 ns | 51.9864 ns |   1.67 |     0.02 |      - |      24 B |
        //                          PixelOperations_Specialized |    Core |  2048 | 1,176.59 ns |   256.955 ns | 14.5184 ns | !!0.28!|     0.00 |      - |       0 B |
    }
}