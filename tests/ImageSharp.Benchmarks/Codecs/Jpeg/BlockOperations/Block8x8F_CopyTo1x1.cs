// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Memory;
// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg.BlockOperations
{
    public class Block8x8F_CopyTo1x1
    {
        private Block8x8F block;

        private Buffer2D<float> buffer;

        private BufferArea<float> destArea;

        [GlobalSetup]
        public void Setup()
        {
            if (!SimdUtils.IsAvx2CompatibleArchitecture)
            {
                throw new InvalidOperationException("Benchmark Block8x8F_CopyTo1x1 is invalid on platforms without AVX2 support.");
            }

            this.buffer = Configuration.Default.MemoryAllocator.Allocate2D<float>(1000, 500);
            this.destArea = this.buffer.GetArea(200, 100, 64, 64);
        }

        [Benchmark(Baseline = true)]
        public void Original()
        {
            ref byte selfBase = ref Unsafe.As<Block8x8F, byte>(ref this.block);
            ref byte destBase = ref Unsafe.As<float, byte>(ref this.destArea.GetReferenceToOrigin());
            int destStride = this.destArea.Stride * sizeof(float);

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

        [Benchmark]
        public void UseVector8()
        {
            ref Block8x8F s = ref this.block;
            ref float origin = ref this.destArea.GetReferenceToOrigin();
            int stride = this.destArea.Stride;

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

        [Benchmark]
        public void UseVector8_V2()
        {
            ref Block8x8F s = ref this.block;
            ref float origin = ref this.destArea.GetReferenceToOrigin();
            int stride = this.destArea.Stride;

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

        // RESULTS:
        //
        //         Method |     Mean |     Error |    StdDev | Scaled |
        // -------------- |---------:|----------:|----------:|-------:|
        //       Original | 22.53 ns | 0.1660 ns | 0.1553 ns |   1.00 |
        //     UseVector8 | 21.59 ns | 0.3079 ns | 0.2571 ns |   0.96 |
        //  UseVector8_V2 | 22.57 ns | 0.1699 ns | 0.1506 ns |   1.00 |
        //
        // Conclusion:
        // Doesn't worth to bother with this
    }
}