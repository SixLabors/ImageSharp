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
    public unsafe class Block8x8F_Round
    {
        private Block8x8F block;

        private readonly byte[] blockBuffer = new byte[512];
        private GCHandle blockHandle;
        private float* alignedPtr;

        [GlobalSetup]
        public void Setup()
        {
            if (Vector<float>.Count != 8)
            {
                throw new NotSupportedException("Vector<float>.Count != 8");
            }

            this.blockHandle = GCHandle.Alloc(this.blockBuffer, GCHandleType.Pinned);
            ulong ptr = (ulong)this.blockHandle.AddrOfPinnedObject();
            ptr += 16;
            ptr -= ptr % 16;

            if (ptr % 16 != 0)
            {
                throw new Exception("ptr is unaligned");
            }

            this.alignedPtr = (float*)ptr;
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.blockHandle.Free();
            this.alignedPtr = null;
        }

        [Benchmark]
        public void ScalarRound()
        {
            ref float b = ref Unsafe.As<Block8x8F, float>(ref this.block);

            for (int i = 0; i < Block8x8F.Size; i++)
            {
                ref float v = ref Unsafe.Add(ref b, i);
                v = (float)Math.Round(v);
            }
        }

        [Benchmark(Baseline = true)]
        public void SimdUtils_FastRound_Vector8()
        {
            ref Block8x8F b = ref this.block;

            ref Vector<float> row0 = ref Unsafe.As<Vector4, Vector<float>>(ref b.V0L);
            row0 = SimdUtils.FastRound(row0);
            ref Vector<float> row1 = ref Unsafe.As<Vector4, Vector<float>>(ref b.V1L);
            row1 = SimdUtils.FastRound(row1);
            ref Vector<float> row2 = ref Unsafe.As<Vector4, Vector<float>>(ref b.V2L);
            row2 = SimdUtils.FastRound(row2);
            ref Vector<float> row3 = ref Unsafe.As<Vector4, Vector<float>>(ref b.V3L);
            row3 = SimdUtils.FastRound(row3);
            ref Vector<float> row4 = ref Unsafe.As<Vector4, Vector<float>>(ref b.V4L);
            row4 = SimdUtils.FastRound(row4);
            ref Vector<float> row5 = ref Unsafe.As<Vector4, Vector<float>>(ref b.V5L);
            row5 = SimdUtils.FastRound(row5);
            ref Vector<float> row6 = ref Unsafe.As<Vector4, Vector<float>>(ref b.V6L);
            row6 = SimdUtils.FastRound(row6);
            ref Vector<float> row7 = ref Unsafe.As<Vector4, Vector<float>>(ref b.V7L);
            row7 = SimdUtils.FastRound(row7);
        }

        [Benchmark]
        public void SimdUtils_FastRound_Vector8_ForceAligned()
        {
            ref Block8x8F b = ref Unsafe.AsRef<Block8x8F>(this.alignedPtr);

            ref Vector<float> row0 = ref Unsafe.As<Vector4, Vector<float>>(ref b.V0L);
            row0 = SimdUtils.FastRound(row0);
            ref Vector<float> row1 = ref Unsafe.As<Vector4, Vector<float>>(ref b.V1L);
            row1 = SimdUtils.FastRound(row1);
            ref Vector<float> row2 = ref Unsafe.As<Vector4, Vector<float>>(ref b.V2L);
            row2 = SimdUtils.FastRound(row2);
            ref Vector<float> row3 = ref Unsafe.As<Vector4, Vector<float>>(ref b.V3L);
            row3 = SimdUtils.FastRound(row3);
            ref Vector<float> row4 = ref Unsafe.As<Vector4, Vector<float>>(ref b.V4L);
            row4 = SimdUtils.FastRound(row4);
            ref Vector<float> row5 = ref Unsafe.As<Vector4, Vector<float>>(ref b.V5L);
            row5 = SimdUtils.FastRound(row5);
            ref Vector<float> row6 = ref Unsafe.As<Vector4, Vector<float>>(ref b.V6L);
            row6 = SimdUtils.FastRound(row6);
            ref Vector<float> row7 = ref Unsafe.As<Vector4, Vector<float>>(ref b.V7L);
            row7 = SimdUtils.FastRound(row7);
        }

        [Benchmark]
        public void SimdUtils_FastRound_Vector8_Grouped()
        {
            ref Block8x8F b = ref this.block;

            ref Vector<float> row0 = ref Unsafe.As<Vector4, Vector<float>>(ref b.V0L);
            ref Vector<float> row1 = ref Unsafe.As<Vector4, Vector<float>>(ref b.V1L);
            ref Vector<float> row2 = ref Unsafe.As<Vector4, Vector<float>>(ref b.V2L);
            ref Vector<float> row3 = ref Unsafe.As<Vector4, Vector<float>>(ref b.V3L);

            row0 = SimdUtils.FastRound(row0);
            row1 = SimdUtils.FastRound(row1);
            row2 = SimdUtils.FastRound(row2);
            row3 = SimdUtils.FastRound(row3);

            row0 = ref Unsafe.As<Vector4, Vector<float>>(ref b.V4L);
            row1 = ref Unsafe.As<Vector4, Vector<float>>(ref b.V5L);
            row2 = ref Unsafe.As<Vector4, Vector<float>>(ref b.V6L);
            row3 = ref Unsafe.As<Vector4, Vector<float>>(ref b.V7L);

            row0 = SimdUtils.FastRound(row0);
            row1 = SimdUtils.FastRound(row1);
            row2 = SimdUtils.FastRound(row2);
            row3 = SimdUtils.FastRound(row3);
        }

#if SUPPORTS_RUNTIME_INTRINSICS
        [Benchmark]
        public void Sse41_V1()
        {
            ref Vector128<float> b0 = ref Unsafe.As<Block8x8F, Vector128<float>>(ref this.block);

            ref Vector128<float> p = ref b0;
            p = Sse41.RoundToNearestInteger(p);

            p = ref Unsafe.Add(ref b0, 1);
            p = Sse41.RoundToNearestInteger(p);
            p = ref Unsafe.Add(ref b0, 2);
            p = Sse41.RoundToNearestInteger(p);
            p = ref Unsafe.Add(ref b0, 3);
            p = Sse41.RoundToNearestInteger(p);
            p = ref Unsafe.Add(ref b0, 4);
            p = Sse41.RoundToNearestInteger(p);
            p = ref Unsafe.Add(ref b0, 5);
            p = Sse41.RoundToNearestInteger(p);
            p = ref Unsafe.Add(ref b0, 6);
            p = Sse41.RoundToNearestInteger(p);
            p = ref Unsafe.Add(ref b0, 7);
            p = Sse41.RoundToNearestInteger(p);
            p = ref Unsafe.Add(ref b0, 8);
            p = Sse41.RoundToNearestInteger(p);
            p = ref Unsafe.Add(ref b0, 9);
            p = Sse41.RoundToNearestInteger(p);
            p = ref Unsafe.Add(ref b0, 10);
            p = Sse41.RoundToNearestInteger(p);
            p = ref Unsafe.Add(ref b0, 11);
            p = Sse41.RoundToNearestInteger(p);
            p = ref Unsafe.Add(ref b0, 12);
            p = Sse41.RoundToNearestInteger(p);
            p = ref Unsafe.Add(ref b0, 13);
            p = Sse41.RoundToNearestInteger(p);
            p = ref Unsafe.Add(ref b0, 14);
            p = Sse41.RoundToNearestInteger(p);
            p = ref Unsafe.Add(ref b0, 15);
            p = Sse41.RoundToNearestInteger(p);
        }

        [Benchmark]
        public unsafe void Sse41_V2()
        {
            ref Vector128<float> p = ref Unsafe.As<Block8x8F, Vector128<float>>(ref this.block);
            p = Sse41.RoundToNearestInteger(p);
            var offset = (IntPtr)sizeof(Vector128<float>);
            p = Sse41.RoundToNearestInteger(p);

            p = ref Unsafe.AddByteOffset(ref p, offset);
            p = Sse41.RoundToNearestInteger(p);
            p = ref Unsafe.AddByteOffset(ref p, offset);
            p = Sse41.RoundToNearestInteger(p);
            p = ref Unsafe.AddByteOffset(ref p, offset);
            p = Sse41.RoundToNearestInteger(p);
            p = ref Unsafe.AddByteOffset(ref p, offset);
            p = Sse41.RoundToNearestInteger(p);
            p = ref Unsafe.AddByteOffset(ref p, offset);
            p = Sse41.RoundToNearestInteger(p);
            p = ref Unsafe.AddByteOffset(ref p, offset);
            p = Sse41.RoundToNearestInteger(p);
            p = ref Unsafe.AddByteOffset(ref p, offset);
            p = Sse41.RoundToNearestInteger(p);
            p = ref Unsafe.AddByteOffset(ref p, offset);
            p = Sse41.RoundToNearestInteger(p);
            p = ref Unsafe.AddByteOffset(ref p, offset);
            p = Sse41.RoundToNearestInteger(p);
            p = ref Unsafe.AddByteOffset(ref p, offset);
            p = Sse41.RoundToNearestInteger(p);
            p = ref Unsafe.AddByteOffset(ref p, offset);
            p = Sse41.RoundToNearestInteger(p);
            p = ref Unsafe.AddByteOffset(ref p, offset);
            p = Sse41.RoundToNearestInteger(p);
            p = ref Unsafe.AddByteOffset(ref p, offset);
            p = Sse41.RoundToNearestInteger(p);
            p = ref Unsafe.AddByteOffset(ref p, offset);
            p = Sse41.RoundToNearestInteger(p);
            p = ref Unsafe.AddByteOffset(ref p, offset);
            p = Sse41.RoundToNearestInteger(p);
        }

        [Benchmark]
        public unsafe void Sse41_V3()
        {
            ref Vector128<float> p = ref Unsafe.As<Block8x8F, Vector128<float>>(ref this.block);
            p = Sse41.RoundToNearestInteger(p);
            var offset = (IntPtr)sizeof(Vector128<float>);

            for (int i = 0; i < 15; i++)
            {
                p = ref Unsafe.AddByteOffset(ref p, offset);
                p = Sse41.RoundToNearestInteger(p);
            }
        }

        [Benchmark]
        public unsafe void Sse41_V4()
        {
            ref Vector128<float> p = ref Unsafe.As<Block8x8F, Vector128<float>>(ref this.block);
            var offset = (IntPtr)sizeof(Vector128<float>);

            ref Vector128<float> a = ref p;
            ref Vector128<float> b = ref Unsafe.AddByteOffset(ref a, offset);
            ref Vector128<float> c = ref Unsafe.AddByteOffset(ref b, offset);
            ref Vector128<float> d = ref Unsafe.AddByteOffset(ref c, offset);
            a = Sse41.RoundToNearestInteger(a);
            b = Sse41.RoundToNearestInteger(b);
            c = Sse41.RoundToNearestInteger(c);
            d = Sse41.RoundToNearestInteger(d);

            a = ref Unsafe.AddByteOffset(ref d, offset);
            b = ref Unsafe.AddByteOffset(ref a, offset);
            c = ref Unsafe.AddByteOffset(ref b, offset);
            d = ref Unsafe.AddByteOffset(ref c, offset);
            a = Sse41.RoundToNearestInteger(a);
            b = Sse41.RoundToNearestInteger(b);
            c = Sse41.RoundToNearestInteger(c);
            d = Sse41.RoundToNearestInteger(d);

            a = ref Unsafe.AddByteOffset(ref d, offset);
            b = ref Unsafe.AddByteOffset(ref a, offset);
            c = ref Unsafe.AddByteOffset(ref b, offset);
            d = ref Unsafe.AddByteOffset(ref c, offset);
            a = Sse41.RoundToNearestInteger(a);
            b = Sse41.RoundToNearestInteger(b);
            c = Sse41.RoundToNearestInteger(c);
            d = Sse41.RoundToNearestInteger(d);

            a = ref Unsafe.AddByteOffset(ref d, offset);
            b = ref Unsafe.AddByteOffset(ref a, offset);
            c = ref Unsafe.AddByteOffset(ref b, offset);
            d = ref Unsafe.AddByteOffset(ref c, offset);
            a = Sse41.RoundToNearestInteger(a);
            b = Sse41.RoundToNearestInteger(b);
            c = Sse41.RoundToNearestInteger(c);
            d = Sse41.RoundToNearestInteger(d);
        }

        [Benchmark]
        public unsafe void Sse41_V5_Unaligned()
        {
            float* p = this.alignedPtr + 1;

            Vector128<float> v = Sse.LoadVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.Store(p, v);
            p += 8;

            v = Sse.LoadVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.Store(p, v);
            p += 8;

            v = Sse.LoadVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.Store(p, v);
            p += 8;

            v = Sse.LoadVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.Store(p, v);
            p += 8;

            v = Sse.LoadVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.Store(p, v);
            p += 8;

            v = Sse.LoadVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.Store(p, v);
            p += 8;

            v = Sse.LoadVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.Store(p, v);
            p += 8;

            v = Sse.LoadVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.Store(p, v);
            p += 8;

            v = Sse.LoadVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.Store(p, v);
            p += 8;

            v = Sse.LoadVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.Store(p, v);
            p += 8;

            v = Sse.LoadVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.Store(p, v);
            p += 8;

            v = Sse.LoadVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.Store(p, v);
            p += 8;

            v = Sse.LoadVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.Store(p, v);
            p += 8;

            v = Sse.LoadVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.Store(p, v);
            p += 8;

            v = Sse.LoadVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.Store(p, v);
            p += 8;

            v = Sse.LoadVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.Store(p, v);
        }

        [Benchmark]
        public unsafe void Sse41_V5_Aligned()
        {
            float* p = this.alignedPtr;

            Vector128<float> v = Sse.LoadAlignedVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.StoreAligned(p, v);
            p += 8;

            v = Sse.LoadAlignedVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.StoreAligned(p, v);
            p += 8;

            v = Sse.LoadAlignedVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.StoreAligned(p, v);
            p += 8;

            v = Sse.LoadAlignedVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.StoreAligned(p, v);
            p += 8;

            v = Sse.LoadAlignedVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.StoreAligned(p, v);
            p += 8;

            v = Sse.LoadAlignedVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.StoreAligned(p, v);
            p += 8;

            v = Sse.LoadAlignedVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.StoreAligned(p, v);
            p += 8;

            v = Sse.LoadAlignedVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.StoreAligned(p, v);
            p += 8;

            v = Sse.LoadAlignedVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.StoreAligned(p, v);
            p += 8;

            v = Sse.LoadAlignedVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.StoreAligned(p, v);
            p += 8;

            v = Sse.LoadAlignedVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.StoreAligned(p, v);
            p += 8;

            v = Sse.LoadAlignedVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.StoreAligned(p, v);
            p += 8;

            v = Sse.LoadAlignedVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.StoreAligned(p, v);
            p += 8;

            v = Sse.LoadAlignedVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.StoreAligned(p, v);
            p += 8;

            v = Sse.LoadAlignedVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.StoreAligned(p, v);
            p += 8;

            v = Sse.LoadAlignedVector128(p);
            v = Sse41.RoundToNearestInteger(v);
            Sse.StoreAligned(p, v);
            p += 8;
        }

        [Benchmark]
        public void Sse41_V6_Aligned()
        {
            float* p = this.alignedPtr;

            Round8SseVectors(p);
            Round8SseVectors(p + 32);
        }

        private static void Round8SseVectors(float* p0)
        {
            float* p1 = p0 + 4;
            float* p2 = p1 + 4;
            float* p3 = p2 + 4;
            float* p4 = p3 + 4;
            float* p5 = p4 + 4;
            float* p6 = p5 + 4;
            float* p7 = p6 + 4;

            Vector128<float> v0 = Sse.LoadAlignedVector128(p0);
            Vector128<float> v1 = Sse.LoadAlignedVector128(p1);
            Vector128<float> v2 = Sse.LoadAlignedVector128(p2);
            Vector128<float> v3 = Sse.LoadAlignedVector128(p3);
            Vector128<float> v4 = Sse.LoadAlignedVector128(p4);
            Vector128<float> v5 = Sse.LoadAlignedVector128(p5);
            Vector128<float> v6 = Sse.LoadAlignedVector128(p6);
            Vector128<float> v7 = Sse.LoadAlignedVector128(p7);

            v0 = Sse41.RoundToNearestInteger(v0);
            v1 = Sse41.RoundToNearestInteger(v1);
            v2 = Sse41.RoundToNearestInteger(v2);
            v3 = Sse41.RoundToNearestInteger(v3);
            v4 = Sse41.RoundToNearestInteger(v4);
            v5 = Sse41.RoundToNearestInteger(v5);
            v6 = Sse41.RoundToNearestInteger(v6);
            v7 = Sse41.RoundToNearestInteger(v7);

            Sse.StoreAligned(p0, v0);
            Sse.StoreAligned(p1, v1);
            Sse.StoreAligned(p2, v2);
            Sse.StoreAligned(p3, v3);
            Sse.StoreAligned(p4, v4);
            Sse.StoreAligned(p5, v5);
            Sse.StoreAligned(p6, v6);
            Sse.StoreAligned(p7, v7);
        }
#endif
    }
}
