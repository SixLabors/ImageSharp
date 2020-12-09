// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.General.PixelConversion
{
    public unsafe class PixelConversion_PackFromRgbPlanes
    {
        private byte[] rBuf;
        private byte[] gBuf;
        private byte[] bBuf;
        private Rgb24[] rgbBuf;
        private Rgba32[] rgbaBuf;

        private float[] rFloat;
        private float[] gFloat;
        private float[] bFloat;

        private float[] rgbaFloat;

        [Params(1024)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.rBuf = new byte[this.Count];
            this.gBuf = new byte[this.Count];
            this.bBuf = new byte[this.Count];
            this.rgbBuf = new Rgb24[this.Count + 3]; // padded
            this.rgbaBuf = new Rgba32[this.Count];

            this.rFloat = new float[this.Count];
            this.gFloat = new float[this.Count];
            this.bFloat = new float[this.Count];

            this.rgbaFloat = new float[this.Count * 4];
        }

        // [Benchmark]
        public void Rgb24_Scalar_PerElement_Pinned()
        {
            fixed (byte* r = &this.rBuf[0])
            fixed (byte* g = &this.gBuf[0])
            fixed (byte* b = &this.bBuf[0])
            fixed (Rgb24* rgb = &this.rgbBuf[0])
            {
                for (int i = 0; i < this.Count; i++)
                {
                    Rgb24* d = rgb + i;
                    d->R = r[i];
                    d->G = g[i];
                    d->B = b[i];
                }
            }
        }

        [Benchmark]
        public void Rgb24_Scalar_PerElement_Span()
        {
            Span<byte> r = this.rBuf;
            Span<byte> g = this.rBuf;
            Span<byte> b = this.rBuf;
            Span<Rgb24> rgb = this.rgbBuf;

            for (int i = 0; i < r.Length; i++)
            {
                ref Rgb24 d = ref rgb[i];
                d.R = r[i];
                d.G = g[i];
                d.B = b[i];
            }
        }

        [Benchmark]
        public void Rgb24_Scalar_PerElement_Unsafe()
        {
            ref byte r = ref this.rBuf[0];
            ref byte g = ref this.rBuf[0];
            ref byte b = ref this.rBuf[0];
            ref Rgb24 rgb = ref this.rgbBuf[0];

            for (int i = 0; i < this.Count; i++)
            {
                ref Rgb24 d = ref Unsafe.Add(ref rgb, i);
                d.R = Unsafe.Add(ref r, i);
                d.G = Unsafe.Add(ref g, i);
                d.B = Unsafe.Add(ref b, i);
            }
        }

        [Benchmark]
        public void Rgb24_Scalar_PerElement_Batched8()
        {
            ref Byte8 r = ref Unsafe.As<byte, Byte8>(ref this.rBuf[0]);
            ref Byte8 g = ref Unsafe.As<byte, Byte8>(ref this.rBuf[0]);
            ref Byte8 b = ref Unsafe.As<byte, Byte8>(ref this.rBuf[0]);
            ref Rgb24 rgb = ref this.rgbBuf[0];

            int count = this.Count / 8;
            for (int i = 0; i < count; i++)
            {
                ref Rgb24 d0 = ref Unsafe.Add(ref rgb, i * 8);
                ref Rgb24 d1 = ref Unsafe.Add(ref d0, 1);
                ref Rgb24 d2 = ref Unsafe.Add(ref d0, 2);
                ref Rgb24 d3 = ref Unsafe.Add(ref d0, 3);
                ref Rgb24 d4 = ref Unsafe.Add(ref d0, 4);
                ref Rgb24 d5 = ref Unsafe.Add(ref d0, 5);
                ref Rgb24 d6 = ref Unsafe.Add(ref d0, 6);
                ref Rgb24 d7 = ref Unsafe.Add(ref d0, 7);

                ref Byte8 rr = ref Unsafe.Add(ref r, i);
                ref Byte8 gg = ref Unsafe.Add(ref g, i);
                ref Byte8 bb = ref Unsafe.Add(ref b, i);

                d0.R = rr.V0;
                d0.G = gg.V0;
                d0.B = bb.V0;

                d1.R = rr.V1;
                d1.G = gg.V1;
                d1.B = bb.V1;

                d2.R = rr.V2;
                d2.G = gg.V2;
                d2.B = bb.V2;

                d3.R = rr.V3;
                d3.G = gg.V3;
                d3.B = bb.V3;

                d4.R = rr.V4;
                d4.G = gg.V4;
                d4.B = bb.V4;

                d5.R = rr.V5;
                d5.G = gg.V5;
                d5.B = bb.V5;

                d6.R = rr.V6;
                d6.G = gg.V6;
                d6.B = bb.V6;

                d7.R = rr.V7;
                d7.G = gg.V7;
                d7.B = bb.V7;
            }
        }

        [Benchmark]
        public void Rgb24_Scalar_PerElement_Batched4()
        {
            ref Byte4 r = ref Unsafe.As<byte, Byte4>(ref this.rBuf[0]);
            ref Byte4 g = ref Unsafe.As<byte, Byte4>(ref this.rBuf[0]);
            ref Byte4 b = ref Unsafe.As<byte, Byte4>(ref this.rBuf[0]);
            ref Rgb24 rgb = ref this.rgbBuf[0];

            int count = this.Count / 4;
            for (int i = 0; i < count; i++)
            {
                ref Rgb24 d0 = ref Unsafe.Add(ref rgb, i * 4);
                ref Rgb24 d1 = ref Unsafe.Add(ref d0, 1);
                ref Rgb24 d2 = ref Unsafe.Add(ref d0, 2);
                ref Rgb24 d3 = ref Unsafe.Add(ref d0, 3);

                ref Byte4 rr = ref Unsafe.Add(ref r, i);
                ref Byte4 gg = ref Unsafe.Add(ref g, i);
                ref Byte4 bb = ref Unsafe.Add(ref b, i);

                d0.R = rr.V0;
                d0.G = gg.V0;
                d0.B = bb.V0;

                d1.R = rr.V1;
                d1.G = gg.V1;
                d1.B = bb.V1;

                d2.R = rr.V2;
                d2.G = gg.V2;
                d2.B = bb.V2;

                d3.R = rr.V3;
                d3.G = gg.V3;
                d3.B = bb.V3;
            }
        }

#if SUPPORTS_RUNTIME_INTRINSICS
        [Benchmark(Baseline = true)]
        public void Rgba32_Avx2_Float()
        {
            ref Vector256<float> rBase = ref Unsafe.As<float, Vector256<float>>(ref this.rFloat[0]);
            ref Vector256<float> gBase = ref Unsafe.As<float, Vector256<float>>(ref this.gFloat[0]);
            ref Vector256<float> bBase = ref Unsafe.As<float, Vector256<float>>(ref this.bFloat[0]);
            ref Vector256<float> resultBase = ref Unsafe.As<float, Vector256<float>>(ref this.rgbaFloat[0]);

            int count = this.Count / Vector256<float>.Count;

            ref byte control = ref MemoryMarshal.GetReference(SimdUtils.HwIntrinsics.PermuteMaskEvenOdd8x32);
            Vector256<int> vcontrol = Unsafe.As<byte, Vector256<int>>(ref control);

            var va = Vector256.Create(1F);

            for (int i = 0; i < count; i++)
            {
                Vector256<float> r = Unsafe.Add(ref rBase, i);
                Vector256<float> g = Unsafe.Add(ref gBase, i);
                Vector256<float> b = Unsafe.Add(ref bBase, i);

                r = Avx2.PermuteVar8x32(r, vcontrol);
                g = Avx2.PermuteVar8x32(g, vcontrol);
                b = Avx2.PermuteVar8x32(b, vcontrol);

                Vector256<float> vte = Avx.UnpackLow(r, b);
                Vector256<float> vto = Avx.UnpackLow(g, va);

                ref Vector256<float> destination = ref Unsafe.Add(ref resultBase, i * 4);

                destination = Avx.UnpackLow(vte, vto);
                Unsafe.Add(ref destination, 1) = Avx.UnpackHigh(vte, vto);

                vte = Avx.UnpackHigh(r, b);
                vto = Avx.UnpackHigh(g, va);

                Unsafe.Add(ref destination, 2) = Avx.UnpackLow(vte, vto);
                Unsafe.Add(ref destination, 3) = Avx.UnpackHigh(vte, vto);
            }
        }

        [Benchmark]
        public void Rgb24_Avx2_Bytes()
        {
            ReadOnlySpan<byte> r = this.rBuf;
            ReadOnlySpan<byte> g = this.rBuf;
            ReadOnlySpan<byte> b = this.rBuf;
            Span<Rgb24> rgb = this.rgbBuf;
            SimdUtils.HwIntrinsics.PackFromRgbPlanesAvx2Reduce(ref r, ref g, ref b, ref rgb);
        }

        [Benchmark]
        public void Rgba32_Avx2_Bytes()
        {
            ReadOnlySpan<byte> r = this.rBuf;
            ReadOnlySpan<byte> g = this.rBuf;
            ReadOnlySpan<byte> b = this.rBuf;
            Span<Rgba32> rgb = this.rgbaBuf;
            SimdUtils.HwIntrinsics.PackFromRgbPlanesAvx2Reduce(ref r, ref g, ref b, ref rgb);
        }
#endif

#pragma warning disable SA1132
        private struct Byte8
        {
            public byte V0, V1, V2, V3, V4, V5, V6, V7;
        }

        private struct Byte4
        {
            public byte V0, V1, V2, V3;
        }
#pragma warning restore

        // Results @ Anton's PC, 2020 Dec 05
        // .NET Core 3.1.1
        // Intel Core i7-7700HQ CPU 2.80GHz (Kaby Lake), 1 CPU, 8 logical and 4 physical cores
        //
        // |                           Method | Count |       Mean |    Error |   StdDev | Ratio | RatioSD |
        // |--------------------------------- |------ |-----------:|---------:|---------:|------:|--------:|
        // |     Rgb24_Scalar_PerElement_Span |  1024 | 1,634.6 ns | 26.56 ns | 24.84 ns |  3.12 |    0.05 |
        // |   Rgb24_Scalar_PerElement_Unsafe |  1024 | 1,284.7 ns |  4.70 ns |  4.16 ns |  2.46 |    0.01 |
        // | Rgb24_Scalar_PerElement_Batched8 |  1024 | 1,182.3 ns |  5.12 ns |  4.27 ns |  2.26 |    0.01 |
        // | Rgb24_Scalar_PerElement_Batched4 |  1024 | 1,146.2 ns | 16.38 ns | 14.52 ns |  2.19 |    0.02 |
        // |                Rgba32_Avx2_Float |  1024 |   522.7 ns |  1.78 ns |  1.39 ns |  1.00 |    0.00 |
        // |                Rgb24_Avx2_Bytes |  1024 |   243.3 ns |  1.56 ns |  1.30 ns |  0.47 |    0.00 |
        // |                Rgba32_Avx2_Bytes |  1024 |   146.0 ns |  2.48 ns |  2.32 ns |  0.28 |    0.01 |
    }
}