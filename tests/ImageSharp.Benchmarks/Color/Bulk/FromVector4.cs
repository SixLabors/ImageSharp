// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Benchmarks.ColorSpaces.Bulk
{
    [Config(typeof(Config.ShortCore31))]
    public abstract class FromVector4<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        protected IMemoryOwner<Vector4> source;

        protected IMemoryOwner<TPixel> destination;

        protected Configuration Configuration => Configuration.Default;

        // [Params(64, 2048)]
        [Params(64, 256, 2048)]
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

        // [Benchmark]
        public void PerElement()
        {
            ref Vector4 s = ref MemoryMarshal.GetReference(this.source.GetSpan());
            ref TPixel d = ref MemoryMarshal.GetReference(this.destination.GetSpan());
            for (int i = 0; i < this.Count; i++)
            {
                Unsafe.Add(ref d, i).FromVector4(Unsafe.Add(ref s, i));
            }
        }

        [Benchmark(Baseline = true)]
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

            SimdUtils.FallbackIntrinsics128.NormalizedFloatToByteSaturate(sBytes, dFloats);
        }

        [Benchmark]
        public void BasicIntrinsics256()
        {
            Span<float> sBytes = MemoryMarshal.Cast<Vector4, float>(this.source.GetSpan());
            Span<byte> dFloats = MemoryMarshal.Cast<Rgba32, byte>(this.destination.GetSpan());

            SimdUtils.BasicIntrinsics256.NormalizedFloatToByteSaturate(sBytes, dFloats);
        }

        [Benchmark]
        public void ExtendedIntrinsic()
        {
            Span<float> sBytes = MemoryMarshal.Cast<Vector4, float>(this.source.GetSpan());
            Span<byte> dFloats = MemoryMarshal.Cast<Rgba32, byte>(this.destination.GetSpan());

            SimdUtils.ExtendedIntrinsics.NormalizedFloatToByteSaturate(sBytes, dFloats);
        }

#if SUPPORTS_RUNTIME_INTRINSICS
        [Benchmark]
        public void UseHwIntrinsics()
        {
            Span<float> sBytes = MemoryMarshal.Cast<Vector4, float>(this.source.GetSpan());
            Span<byte> dFloats = MemoryMarshal.Cast<Rgba32, byte>(this.destination.GetSpan());

            SimdUtils.HwIntrinsics.NormalizedFloatToByteSaturate(sBytes, dFloats);
        }

        private static ReadOnlySpan<byte> PermuteMaskDeinterleave8x32 => new byte[] { 0, 0, 0, 0, 4, 0, 0, 0, 1, 0, 0, 0, 5, 0, 0, 0, 2, 0, 0, 0, 6, 0, 0, 0, 3, 0, 0, 0, 7, 0, 0, 0 };

        [Benchmark]
        public void UseAvx2_Grouped()
        {
            Span<float> src = MemoryMarshal.Cast<Vector4, float>(this.source.GetSpan());
            Span<byte> dest = MemoryMarshal.Cast<Rgba32, byte>(this.destination.GetSpan());

            int n = dest.Length / Vector<byte>.Count;

            ref Vector256<float> sourceBase =
                ref Unsafe.As<float, Vector256<float>>(ref MemoryMarshal.GetReference(src));
            ref Vector256<byte> destBase = ref Unsafe.As<byte, Vector256<byte>>(ref MemoryMarshal.GetReference(dest));

            ref byte maskBase = ref MemoryMarshal.GetReference(PermuteMaskDeinterleave8x32);
            Vector256<int> mask = Unsafe.As<byte, Vector256<int>>(ref maskBase);

            var maxBytes = Vector256.Create(255f);

            for (int i = 0; i < n; i++)
            {
                ref Vector256<float> s = ref Unsafe.Add(ref sourceBase, i * 4);

                Vector256<float> f0 = s;
                Vector256<float> f1 = Unsafe.Add(ref s, 1);
                Vector256<float> f2 = Unsafe.Add(ref s, 2);
                Vector256<float> f3 = Unsafe.Add(ref s, 3);

                f0 = Avx.Multiply(maxBytes, f0);
                f1 = Avx.Multiply(maxBytes, f1);
                f2 = Avx.Multiply(maxBytes, f2);
                f3 = Avx.Multiply(maxBytes, f3);

                Vector256<int> w0 = Avx.ConvertToVector256Int32(f0);
                Vector256<int> w1 = Avx.ConvertToVector256Int32(f1);
                Vector256<int> w2 = Avx.ConvertToVector256Int32(f2);
                Vector256<int> w3 = Avx.ConvertToVector256Int32(f3);

                Vector256<short> u0 = Avx2.PackSignedSaturate(w0, w1);
                Vector256<short> u1 = Avx2.PackSignedSaturate(w2, w3);
                Vector256<byte> b = Avx2.PackUnsignedSaturate(u0, u1);
                b = Avx2.PermuteVar8x32(b.AsInt32(), mask).AsByte();

                Unsafe.Add(ref destBase, i) = b;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<int> ConvertToInt32(Vector256<float> vf, Vector256<float> scale)
        {
            vf = Avx.Multiply(scale, vf);
            return Avx.ConvertToVector256Int32(vf);
        }
#endif

        // *** RESULTS 2020 March: ***
        // Intel Core i7-8650U CPU 1.90GHz (Kaby Lake R), 1 CPU, 8 logical and 4 physical cores
        // .NET Core SDK=3.1.200-preview-014971
        //   Job-IUZXZT : .NET Core 3.1.2 (CoreCLR 4.700.20.6602, CoreFX 4.700.20.6702), X64 RyuJIT
        //
        // |                      Method | Count |       Mean |       Error |    StdDev | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
        // |---------------------------- |------ |-----------:|------------:|----------:|------:|--------:|------:|------:|------:|----------:|
        // |       FallbackIntrinsics128 |  1024 | 2,952.6 ns | 1,680.77 ns |  92.13 ns |  3.32 |    0.16 |     - |     - |     - |         - |
        // |          BasicIntrinsics256 |  1024 | 1,664.5 ns |   928.11 ns |  50.87 ns |  1.87 |    0.09 |     - |     - |     - |         - |
        // |           ExtendedIntrinsic |  1024 |   890.6 ns |   375.48 ns |  20.58 ns |  1.00 |    0.00 |     - |     - |     - |         - |
        // |                     UseAvx2 |  1024 |   299.0 ns |    30.47 ns |   1.67 ns |  0.34 |    0.01 |     - |     - |     - |         - |
        // |             UseAvx2_Grouped |  1024 |   318.1 ns |    48.19 ns |   2.64 ns |  0.36 |    0.01 |     - |     - |     - |         - |
        // |        PixelOperations_Base |  1024 | 8,136.9 ns | 1,834.82 ns | 100.57 ns |  9.14 |    0.26 |     - |     - |     - |      24 B |
        // | PixelOperations_Specialized |  1024 |   951.1 ns |   123.93 ns |   6.79 ns |  1.07 |    0.03 |     - |     - |     - |         - |
    }
}
