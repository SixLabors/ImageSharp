// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#if SUPPORTS_RUNTIME_INTRINSICS
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.Formats.Jpeg.Components;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg.BlockOperations
{
    public unsafe class Block8x8F_CopyTo1x1
    {
        private Block8x8F block;
        private readonly Block8x8F[] blockArray = new Block8x8F[1];

        private static readonly int Width = 100;

        private float[] buffer = new float[Width * 500];
        private readonly float[] unpinnedBuffer = new float[Width * 500];
        private GCHandle bufferHandle;
        private GCHandle blockHandle;
        private float* bufferPtr;
        private float* blockPtr;

        [GlobalSetup]
        public void Setup()
        {
            if (!SimdUtils.HasVector8)
            {
                throw new InvalidOperationException("Benchmark Block8x8F_CopyTo1x1 is invalid on platforms without AVX2 support.");
            }

            this.bufferHandle = GCHandle.Alloc(this.buffer, GCHandleType.Pinned);
            this.bufferPtr = (float*)this.bufferHandle.AddrOfPinnedObject();

            // Pin self so we can take address of to the block:
            this.blockHandle = GCHandle.Alloc(this.blockArray, GCHandleType.Pinned);
            this.blockPtr = (float*)Unsafe.AsPointer(ref this.block);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.bufferPtr = null;
            this.blockPtr = null;
            this.bufferHandle.Free();
            this.blockHandle.Free();
            this.buffer = null;
        }

        [Benchmark(Baseline = true)]
        public void Original()
        {
            ref byte selfBase = ref Unsafe.As<Block8x8F, byte>(ref this.block);
            ref byte destBase = ref Unsafe.AsRef<byte>(this.bufferPtr);
            int destStride = Width * sizeof(float);

            CopyRowImpl(ref selfBase, ref destBase, destStride, 0);
            CopyRowImpl(ref selfBase, ref destBase, destStride, 1);
            CopyRowImpl(ref selfBase, ref destBase, destStride, 2);
            CopyRowImpl(ref selfBase, ref destBase, destStride, 3);
            CopyRowImpl(ref selfBase, ref destBase, destStride, 4);
            CopyRowImpl(ref selfBase, ref destBase, destStride, 5);
            CopyRowImpl(ref selfBase, ref destBase, destStride, 6);
            CopyRowImpl(ref selfBase, ref destBase, destStride, 7);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CopyRowImpl(ref byte selfBase, ref byte destBase, int destStride, int row)
        {
            ref byte s = ref Unsafe.Add(ref selfBase, row * 8 * sizeof(float));
            ref byte d = ref Unsafe.Add(ref destBase, row * destStride);
            Unsafe.CopyBlock(ref d, ref s, 8 * sizeof(float));
        }

        // [Benchmark]
        public void UseVector8()
        {
            ref Block8x8F s = ref this.block;
            ref float origin = ref Unsafe.AsRef<float>(this.bufferPtr);
            int stride = Width;

            ref Vector<float> d0 = ref Unsafe.As<float, Vector<float>>(ref origin);
            ref Vector<float> d1 = ref Unsafe.As<float, Vector<float>>(ref Unsafe.Add(ref origin, stride));
            ref Vector<float> d2 = ref Unsafe.As<float, Vector<float>>(ref Unsafe.Add(ref origin, stride * 2));
            ref Vector<float> d3 = ref Unsafe.As<float, Vector<float>>(ref Unsafe.Add(ref origin, stride * 3));
            ref Vector<float> d4 = ref Unsafe.As<float, Vector<float>>(ref Unsafe.Add(ref origin, stride * 4));
            ref Vector<float> d5 = ref Unsafe.As<float, Vector<float>>(ref Unsafe.Add(ref origin, stride * 5));
            ref Vector<float> d6 = ref Unsafe.As<float, Vector<float>>(ref Unsafe.Add(ref origin, stride * 6));
            ref Vector<float> d7 = ref Unsafe.As<float, Vector<float>>(ref Unsafe.Add(ref origin, stride * 7));

            Vector<float> row0 = Unsafe.As<Vector4, Vector<float>>(ref s.V0L);
            Vector<float> row1 = Unsafe.As<Vector4, Vector<float>>(ref s.V1L);
            Vector<float> row2 = Unsafe.As<Vector4, Vector<float>>(ref s.V2L);
            Vector<float> row3 = Unsafe.As<Vector4, Vector<float>>(ref s.V3L);
            Vector<float> row4 = Unsafe.As<Vector4, Vector<float>>(ref s.V4L);
            Vector<float> row5 = Unsafe.As<Vector4, Vector<float>>(ref s.V5L);
            Vector<float> row6 = Unsafe.As<Vector4, Vector<float>>(ref s.V6L);
            Vector<float> row7 = Unsafe.As<Vector4, Vector<float>>(ref s.V7L);

            d0 = row0;
            d1 = row1;
            d2 = row2;
            d3 = row3;
            d4 = row4;
            d5 = row5;
            d6 = row6;
            d7 = row7;
        }

        // [Benchmark]
        public void UseVector8_V2()
        {
            ref Block8x8F s = ref this.block;
            ref float origin = ref Unsafe.AsRef<float>(this.bufferPtr);
            int stride = Width;

            ref Vector<float> d0 = ref Unsafe.As<float, Vector<float>>(ref origin);
            ref Vector<float> d1 = ref Unsafe.As<float, Vector<float>>(ref Unsafe.Add(ref origin, stride));
            ref Vector<float> d2 = ref Unsafe.As<float, Vector<float>>(ref Unsafe.Add(ref origin, stride * 2));
            ref Vector<float> d3 = ref Unsafe.As<float, Vector<float>>(ref Unsafe.Add(ref origin, stride * 3));
            ref Vector<float> d4 = ref Unsafe.As<float, Vector<float>>(ref Unsafe.Add(ref origin, stride * 4));
            ref Vector<float> d5 = ref Unsafe.As<float, Vector<float>>(ref Unsafe.Add(ref origin, stride * 5));
            ref Vector<float> d6 = ref Unsafe.As<float, Vector<float>>(ref Unsafe.Add(ref origin, stride * 6));
            ref Vector<float> d7 = ref Unsafe.As<float, Vector<float>>(ref Unsafe.Add(ref origin, stride * 7));

            d0 = Unsafe.As<Vector4, Vector<float>>(ref s.V0L);
            d1 = Unsafe.As<Vector4, Vector<float>>(ref s.V1L);
            d2 = Unsafe.As<Vector4, Vector<float>>(ref s.V2L);
            d3 = Unsafe.As<Vector4, Vector<float>>(ref s.V3L);
            d4 = Unsafe.As<Vector4, Vector<float>>(ref s.V4L);
            d5 = Unsafe.As<Vector4, Vector<float>>(ref s.V5L);
            d6 = Unsafe.As<Vector4, Vector<float>>(ref s.V6L);
            d7 = Unsafe.As<Vector4, Vector<float>>(ref s.V7L);
        }

        [Benchmark]
        public void UseVector8_V3()
        {
            int stride = Width * sizeof(float);
            ref float d = ref this.unpinnedBuffer[0];
            ref Vector<float> s = ref Unsafe.As<Block8x8F, Vector<float>>(ref this.block);

            Vector<float> v0 = s;
            Vector<float> v1 = Unsafe.AddByteOffset(ref s, (IntPtr)1);
            Vector<float> v2 = Unsafe.AddByteOffset(ref s, (IntPtr)2);
            Vector<float> v3 = Unsafe.AddByteOffset(ref s, (IntPtr)3);

            Unsafe.As<float, Vector<float>>(ref d) = v0;
            Unsafe.As<float, Vector<float>>(ref Unsafe.AddByteOffset(ref d, (IntPtr)stride)) = v1;
            Unsafe.As<float, Vector<float>>(ref Unsafe.AddByteOffset(ref d, (IntPtr)(stride * 2))) = v2;
            Unsafe.As<float, Vector<float>>(ref Unsafe.AddByteOffset(ref d, (IntPtr)(stride * 3))) = v3;

            v0 = Unsafe.AddByteOffset(ref s, (IntPtr)4);
            v1 = Unsafe.AddByteOffset(ref s, (IntPtr)5);
            v2 = Unsafe.AddByteOffset(ref s, (IntPtr)6);
            v3 = Unsafe.AddByteOffset(ref s, (IntPtr)7);

            Unsafe.As<float, Vector<float>>(ref Unsafe.AddByteOffset(ref d, (IntPtr)(stride * 4))) = v0;
            Unsafe.As<float, Vector<float>>(ref Unsafe.AddByteOffset(ref d, (IntPtr)(stride * 5))) = v1;
            Unsafe.As<float, Vector<float>>(ref Unsafe.AddByteOffset(ref d, (IntPtr)(stride * 6))) = v2;
            Unsafe.As<float, Vector<float>>(ref Unsafe.AddByteOffset(ref d, (IntPtr)(stride * 7))) = v3;
        }

#if SUPPORTS_RUNTIME_INTRINSICS
        [Benchmark]
        public void UseVector256_Avx2_Variant1()
        {
            int stride = Width;
            float* d = this.bufferPtr;
            float* s = this.blockPtr;
            Vector256<float> v;

            v = Avx.LoadVector256(s);
            Avx.Store(d, v);

            v = Avx.LoadVector256(s + 8);
            Avx.Store(d + stride, v);

            v = Avx.LoadVector256(s + (8 * 2));
            Avx.Store(d + (stride * 2), v);

            v = Avx.LoadVector256(s + (8 * 3));
            Avx.Store(d + (stride * 3), v);

            v = Avx.LoadVector256(s + (8 * 4));
            Avx.Store(d + (stride * 4), v);

            v = Avx.LoadVector256(s + (8 * 5));
            Avx.Store(d + (stride * 5), v);

            v = Avx.LoadVector256(s + (8 * 6));
            Avx.Store(d + (stride * 6), v);

            v = Avx.LoadVector256(s + (8 * 7));
            Avx.Store(d + (stride * 7), v);
        }

        [Benchmark]
        public void UseVector256_Avx2_Variant2()
        {
            int stride = Width;
            float* d = this.bufferPtr;
            float* s = this.blockPtr;

            Vector256<float> v0 = Avx.LoadVector256(s);
            Vector256<float> v1 = Avx.LoadVector256(s + 8);
            Vector256<float> v2 = Avx.LoadVector256(s + (8 * 2));
            Vector256<float> v3 = Avx.LoadVector256(s + (8 * 3));
            Vector256<float> v4 = Avx.LoadVector256(s + (8 * 4));
            Vector256<float> v5 = Avx.LoadVector256(s + (8 * 5));
            Vector256<float> v6 = Avx.LoadVector256(s + (8 * 6));
            Vector256<float> v7 = Avx.LoadVector256(s + (8 * 7));

            Avx.Store(d, v0);
            Avx.Store(d + stride, v1);
            Avx.Store(d + (stride * 2), v2);
            Avx.Store(d + (stride * 3), v3);
            Avx.Store(d + (stride * 4), v4);
            Avx.Store(d + (stride * 5), v5);
            Avx.Store(d + (stride * 6), v6);
            Avx.Store(d + (stride * 7), v7);
        }

        [Benchmark]
        public void UseVector256_Avx2_Variant3()
        {
            int stride = Width;
            float* d = this.bufferPtr;
            float* s = this.blockPtr;

            Vector256<float> v0 = Avx.LoadVector256(s);
            Vector256<float> v1 = Avx.LoadVector256(s + 8);
            Vector256<float> v2 = Avx.LoadVector256(s + (8 * 2));
            Vector256<float> v3 = Avx.LoadVector256(s + (8 * 3));
            Avx.Store(d, v0);
            Avx.Store(d + stride, v1);
            Avx.Store(d + (stride * 2), v2);
            Avx.Store(d + (stride * 3), v3);

            v0 = Avx.LoadVector256(s + (8 * 4));
            v1 = Avx.LoadVector256(s + (8 * 5));
            v2 = Avx.LoadVector256(s + (8 * 6));
            v3 = Avx.LoadVector256(s + (8 * 7));
            Avx.Store(d + (stride * 4), v0);
            Avx.Store(d + (stride * 5), v1);
            Avx.Store(d + (stride * 6), v2);
            Avx.Store(d + (stride * 7), v3);
        }

        [Benchmark]
        public void UseVector256_Avx2_Variant3_RefCast()
        {
            int stride = Width;
            ref float d = ref this.unpinnedBuffer[0];
            ref Vector256<float> s = ref Unsafe.As<Block8x8F, Vector256<float>>(ref this.block);

            Vector256<float> v0 = s;
            Vector256<float> v1 = Unsafe.Add(ref s, 1);
            Vector256<float> v2 = Unsafe.Add(ref s, 2);
            Vector256<float> v3 = Unsafe.Add(ref s, 3);

            Unsafe.As<float, Vector256<float>>(ref d) = v0;
            Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref d, stride)) = v1;
            Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref d, stride * 2)) = v2;
            Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref d, stride * 3)) = v3;

            v0 = Unsafe.Add(ref s, 4);
            v1 = Unsafe.Add(ref s, 5);
            v2 = Unsafe.Add(ref s, 6);
            v3 = Unsafe.Add(ref s, 7);

            Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref d, stride * 4)) = v0;
            Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref d, stride * 5)) = v1;
            Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref d, stride * 6)) = v2;
            Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref d, stride * 7)) = v3;
        }

        [Benchmark]
        public void UseVector256_Avx2_Variant3_RefCast_Mod()
        {
            int stride = Width * sizeof(float);
            ref float d = ref this.unpinnedBuffer[0];
            ref Vector256<float> s = ref Unsafe.As<Block8x8F, Vector256<float>>(ref this.block);

            Vector256<float> v0 = s;
            Vector256<float> v1 = Unsafe.AddByteOffset(ref s, (IntPtr)1);
            Vector256<float> v2 = Unsafe.AddByteOffset(ref s, (IntPtr)2);
            Vector256<float> v3 = Unsafe.AddByteOffset(ref s, (IntPtr)3);

            Unsafe.As<float, Vector256<float>>(ref d) = v0;
            Unsafe.As<float, Vector256<float>>(ref Unsafe.AddByteOffset(ref d, (IntPtr)stride)) = v1;
            Unsafe.As<float, Vector256<float>>(ref Unsafe.AddByteOffset(ref d, (IntPtr)(stride * 2))) = v2;
            Unsafe.As<float, Vector256<float>>(ref Unsafe.AddByteOffset(ref d, (IntPtr)(stride * 3))) = v3;

            v0 = Unsafe.AddByteOffset(ref s, (IntPtr)4);
            v1 = Unsafe.AddByteOffset(ref s, (IntPtr)5);
            v2 = Unsafe.AddByteOffset(ref s, (IntPtr)6);
            v3 = Unsafe.AddByteOffset(ref s, (IntPtr)7);

            Unsafe.As<float, Vector256<float>>(ref Unsafe.AddByteOffset(ref d, (IntPtr)(stride * 4))) = v0;
            Unsafe.As<float, Vector256<float>>(ref Unsafe.AddByteOffset(ref d, (IntPtr)(stride * 5))) = v1;
            Unsafe.As<float, Vector256<float>>(ref Unsafe.AddByteOffset(ref d, (IntPtr)(stride * 6))) = v2;
            Unsafe.As<float, Vector256<float>>(ref Unsafe.AddByteOffset(ref d, (IntPtr)(stride * 7))) = v3;
        }

        // [Benchmark]
        public void UseVector256_Avx2_Variant3_WithLocalPinning()
        {
            int stride = Width;
            fixed (float* d = this.unpinnedBuffer)
            fixed (Block8x8F* ss = &this.block)
            {
                var s = (float*)ss;
                Vector256<float> v0 = Avx.LoadVector256(s);
                Vector256<float> v1 = Avx.LoadVector256(s + 8);
                Vector256<float> v2 = Avx.LoadVector256(s + (8 * 2));
                Vector256<float> v3 = Avx.LoadVector256(s + (8 * 3));
                Avx.Store(d, v0);
                Avx.Store(d + stride, v1);
                Avx.Store(d + (stride * 2), v2);
                Avx.Store(d + (stride * 3), v3);

                v0 = Avx.LoadVector256(s + (8 * 4));
                v1 = Avx.LoadVector256(s + (8 * 5));
                v2 = Avx.LoadVector256(s + (8 * 6));
                v3 = Avx.LoadVector256(s + (8 * 7));
                Avx.Store(d + (stride * 4), v0);
                Avx.Store(d + (stride * 5), v1);
                Avx.Store(d + (stride * 6), v2);
                Avx.Store(d + (stride * 7), v3);
            }
        }

        // [Benchmark]
        public void UseVector256_Avx2_Variant3_sbyte()
        {
            int stride = Width * 4;
            var d = (sbyte*)this.bufferPtr;
            var s = (sbyte*)this.blockPtr;

            Vector256<sbyte> v0 = Avx.LoadVector256(s);
            Vector256<sbyte> v1 = Avx.LoadVector256(s + 32);
            Vector256<sbyte> v2 = Avx.LoadVector256(s + (32 * 2));
            Vector256<sbyte> v3 = Avx.LoadVector256(s + (32 * 3));
            Avx.Store(d, v0);
            Avx.Store(d + stride, v1);
            Avx.Store(d + (stride * 2), v2);
            Avx.Store(d + (stride * 3), v3);

            v0 = Avx.LoadVector256(s + (32 * 4));
            v1 = Avx.LoadVector256(s + (32 * 5));
            v2 = Avx.LoadVector256(s + (32 * 6));
            v3 = Avx.LoadVector256(s + (32 * 7));
            Avx.Store(d + (stride * 4), v0);
            Avx.Store(d + (stride * 5), v1);
            Avx.Store(d + (stride * 6), v2);
            Avx.Store(d + (stride * 7), v3);
        }
#endif

        // *** RESULTS 02/2020 ***
        // BenchmarkDotNet=v0.12.0, OS=Windows 10.0.18363
        // Intel Core i7-8650U CPU 1.90GHz (Kaby Lake R), 1 CPU, 8 logical and 4 physical cores
        // .NET Core SDK=3.1.200-preview-014971
        //   [Host]     : .NET Core 3.1.2 (CoreCLR 4.700.20.6602, CoreFX 4.700.20.6702), X64 RyuJIT
        //   DefaultJob : .NET Core 3.1.2 (CoreCLR 4.700.20.6602, CoreFX 4.700.20.6702), X64 RyuJIT
        //
        //
        // |                                 Method |     Mean |     Error |    StdDev | Ratio | RatioSD |
        // |--------------------------------------- |---------:|----------:|----------:|------:|--------:|
        // |                               Original | 4.012 ns | 0.0567 ns | 0.0531 ns |  1.00 |    0.00 |
        // |                          UseVector8_V3 | 4.013 ns | 0.0947 ns | 0.0840 ns |  1.00 |    0.03 |
        // |             UseVector256_Avx2_Variant1 | 2.546 ns | 0.0376 ns | 0.0314 ns |  0.63 |    0.01 |
        // |             UseVector256_Avx2_Variant2 | 2.643 ns | 0.0162 ns | 0.0151 ns |  0.66 |    0.01 |
        // |             UseVector256_Avx2_Variant3 | 2.520 ns | 0.0760 ns | 0.0813 ns |  0.63 |    0.02 |
        // |     UseVector256_Avx2_Variant3_RefCast | 2.300 ns | 0.0877 ns | 0.0938 ns |  0.58 |    0.03 |
        // | UseVector256_Avx2_Variant3_RefCast_Mod | 2.139 ns | 0.0698 ns | 0.0686 ns |  0.53 |    0.02 |
    }
}
