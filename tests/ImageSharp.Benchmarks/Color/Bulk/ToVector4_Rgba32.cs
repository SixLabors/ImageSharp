// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.ColorSpaces.Bulk
{
    [Config(typeof(Config.ShortCore31))]
    public class ToVector4_Rgba32 : ToVector4<Rgba32>
    {
        [Benchmark]
        public void FallbackIntrinsics128()
        {
            Span<byte> sBytes = MemoryMarshal.Cast<Rgba32, byte>(this.source.GetSpan());
            Span<float> dFloats = MemoryMarshal.Cast<Vector4, float>(this.destination.GetSpan());

            SimdUtils.FallbackIntrinsics128.ByteToNormalizedFloat(sBytes, dFloats);
        }

        [Benchmark]
        public void PixelOperations_Base()
        {
            new PixelOperations<Rgba32>().ToVector4(
                this.Configuration,
                this.source.GetSpan(),
                this.destination.GetSpan());
        }

        [Benchmark(Baseline = true)]
        public void BasicIntrinsics256()
        {
            Span<byte> sBytes = MemoryMarshal.Cast<Rgba32, byte>(this.source.GetSpan());
            Span<float> dFloats = MemoryMarshal.Cast<Vector4, float>(this.destination.GetSpan());

            SimdUtils.BasicIntrinsics256.ByteToNormalizedFloat(sBytes, dFloats);
        }

        [Benchmark]
        public void ExtendedIntrinsics()
        {
            Span<byte> sBytes = MemoryMarshal.Cast<Rgba32, byte>(this.source.GetSpan());
            Span<float> dFloats = MemoryMarshal.Cast<Vector4, float>(this.destination.GetSpan());

            SimdUtils.ExtendedIntrinsics.ByteToNormalizedFloat(sBytes, dFloats);
        }

#if SUPPORTS_RUNTIME_INTRINSICS
        [Benchmark]
        public void HwIntrinsics()
        {
            Span<byte> sBytes = MemoryMarshal.Cast<Rgba32, byte>(this.source.GetSpan());
            Span<float> dFloats = MemoryMarshal.Cast<Vector4, float>(this.destination.GetSpan());

            SimdUtils.HwIntrinsics.ByteToNormalizedFloat(sBytes, dFloats);
        }
#endif

        // [Benchmark]
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

        // [Benchmark]
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
            var vi = Vector.AsVectorInt32(u);
            var v = Vector.ConvertToSingle(vi);
            v *= scale;
            return v;
        }

        /*RESULTS (2018 October):

                              Method | Runtime | Count |        Mean |        Error |      StdDev | Scaled | ScaledSD |  Gen 0 | Allocated |
        ---------------------------- |-------- |------ |------------:|-------------:|------------:|-------:|---------:|-------:|----------:|
               FallbackIntrinsics128 |     Clr |    64 |   287.62 ns |     6.026 ns |   0.3405 ns |   1.19 |     0.00 |      - |       0 B |
                  BasicIntrinsics256 |     Clr |    64 |   240.83 ns |    10.585 ns |   0.5981 ns |   1.00 |     0.00 |      - |       0 B |
                  ExtendedIntrinsics |     Clr |    64 |   168.28 ns |    11.478 ns |   0.6485 ns |   0.70 |     0.00 |      - |       0 B |
                PixelOperations_Base |     Clr |    64 |   334.08 ns |    38.048 ns |   2.1498 ns |   1.39 |     0.01 | 0.0072 |      24 B |
         PixelOperations_Specialized |     Clr |    64 |   255.41 ns |    10.939 ns |   0.6181 ns |   1.06 |     0.00 |      - |       0 B | <--- ceremonial overhead has been minimized!
                                     |         |       |             |              |             |        |          |        |           |
               FallbackIntrinsics128 |    Core |    64 |   183.29 ns |     8.931 ns |   0.5046 ns |   1.32 |     0.00 |      - |       0 B |
                  BasicIntrinsics256 |    Core |    64 |   139.18 ns |     7.633 ns |   0.4313 ns |   1.00 |     0.00 |      - |       0 B |
                  ExtendedIntrinsics |    Core |    64 |    66.29 ns |    16.366 ns |   0.9247 ns |   0.48 |     0.01 |      - |       0 B |
                PixelOperations_Base |    Core |    64 |   257.75 ns |    16.959 ns |   0.9582 ns |   1.85 |     0.01 | 0.0072 |      24 B |
         PixelOperations_Specialized |    Core |    64 |    90.14 ns |     9.955 ns |   0.5625 ns |   0.65 |     0.00 |      - |       0 B |
                                     |         |       |             |              |             |        |          |        |           |
               FallbackIntrinsics128 |     Clr |  2048 | 5,011.84 ns |   347.991 ns |  19.6621 ns |   1.22 |     0.01 |      - |       0 B |
                  BasicIntrinsics256 |     Clr |  2048 | 4,119.35 ns |   720.153 ns |  40.6900 ns |   1.00 |     0.00 |      - |       0 B |
                  ExtendedIntrinsics |     Clr |  2048 | 1,195.29 ns |   164.389 ns |   9.2883 ns |!! 0.29 |     0.00 |      - |       0 B | <--- ExtendedIntrinsics rock!
                PixelOperations_Base |     Clr |  2048 | 6,820.58 ns |   823.433 ns |  46.5255 ns |   1.66 |     0.02 |      - |      24 B |
         PixelOperations_Specialized |     Clr |  2048 | 4,203.53 ns |   176.714 ns |   9.9847 ns |   1.02 |     0.01 |      - |       0 B | <--- can't yet detect whether ExtendedIntrinsics are available :(
                                     |         |       |             |              |             |        |          |        |           |
               FallbackIntrinsics128 |    Core |  2048 | 5,017.89 ns | 4,021.533 ns | 227.2241 ns |   1.24 |     0.05 |      - |       0 B |
                  BasicIntrinsics256 |    Core |  2048 | 4,046.51 ns | 1,150.390 ns |  64.9992 ns |   1.00 |     0.00 |      - |       0 B |
                  ExtendedIntrinsics |    Core |  2048 | 1,130.59 ns |   832.588 ns |  47.0427 ns |!! 0.28 |     0.01 |      - |       0 B | <--- ExtendedIntrinsics rock!
                PixelOperations_Base |    Core |  2048 | 6,752.68 ns |   272.820 ns |  15.4148 ns |   1.67 |     0.02 |      - |      24 B |
         PixelOperations_Specialized |    Core |  2048 | 1,126.13 ns |    79.192 ns |   4.4745 ns |!! 0.28 |     0.00 |      - |       0 B | <--- ExtendedIntrinsics rock!
         */
    }
}
