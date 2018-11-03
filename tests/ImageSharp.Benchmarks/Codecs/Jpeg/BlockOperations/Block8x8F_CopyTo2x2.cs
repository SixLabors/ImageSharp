// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;

using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Memory;
// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg.BlockOperations
{
    public class Block8x8F_CopyTo2x2
    {
        private Block8x8F block;

        private Buffer2D<float> buffer;

        private BufferArea<float> destArea;

        [GlobalSetup]
        public void Setup()
        {
            this.buffer = Configuration.Default.MemoryAllocator.Allocate2D<float>(1000, 500);
            this.destArea = this.buffer.GetArea(200, 100, 128, 128);
        }

        [Benchmark(Baseline = true)]
        public void Original()
        {
            ref float destBase = ref this.destArea.GetReferenceToOrigin();
            int destStride = this.destArea.Stride;

            ref Block8x8F src = ref this.block;

            WidenCopyImpl2x2(ref src, ref destBase, 0, destStride);
            WidenCopyImpl2x2(ref src, ref destBase, 1, destStride);
            WidenCopyImpl2x2(ref src, ref destBase, 2, destStride);
            WidenCopyImpl2x2(ref src, ref destBase, 3, destStride);
            WidenCopyImpl2x2(ref src, ref destBase, 4, destStride);
            WidenCopyImpl2x2(ref src, ref destBase, 5, destStride);
            WidenCopyImpl2x2(ref src, ref destBase, 6, destStride);
            WidenCopyImpl2x2(ref src, ref destBase, 7, destStride);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WidenCopyImpl2x2(ref Block8x8F src, ref float destBase, int row, int destStride)
        {
            ref Vector4 selfRight = ref Unsafe.Add(ref src.V0L, 2 * row);
            ref Vector4 selfRight = ref Unsafe.Add(ref selfLeft, 1);
            ref float destLocalOrigo = ref Unsafe.Add(ref destBase, row * 2 * destStride);

            Unsafe.Add(ref destLocalOrigo, 0) = selfLeft.X;
            Unsafe.Add(ref destLocalOrigo, 1) = selfLeft.X;
            Unsafe.Add(ref destLocalOrigo, 2) = selfLeft.Y;
            Unsafe.Add(ref destLocalOrigo, 3) = selfLeft.Y;
            Unsafe.Add(ref destLocalOrigo, 4) = selfLeft.Z;
            Unsafe.Add(ref destLocalOrigo, 5) = selfLeft.Z;
            Unsafe.Add(ref destLocalOrigo, 6) = selfLeft.W;
            Unsafe.Add(ref destLocalOrigo, 7) = selfLeft.W;

            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, 8), 0) = selfRight.X;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, 8), 1) = selfRight.X;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, 8), 2) = selfRight.Y;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, 8), 3) = selfRight.Y;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, 8), 4) = selfRight.Z;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, 8), 5) = selfRight.Z;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, 8), 6) = selfRight.W;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, 8), 7) = selfRight.W;

            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride), 0) = selfLeft.X;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride), 1) = selfLeft.X;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride), 2) = selfLeft.Y;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride), 3) = selfLeft.Y;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride), 4) = selfLeft.Z;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride), 5) = selfLeft.Z;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride), 6) = selfLeft.W;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride), 7) = selfLeft.W;

            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride + 8), 0) = selfRight.X;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride + 8), 1) = selfRight.X;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride + 8), 2) = selfRight.Y;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride + 8), 3) = selfRight.Y;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride + 8), 4) = selfRight.Z;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride + 8), 5) = selfRight.Z;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride + 8), 6) = selfRight.W;
            Unsafe.Add(ref Unsafe.Add(ref destLocalOrigo, destStride + 8), 7) = selfRight.W;
        }

        [Benchmark]
        public void Original_V2()
        {
            ref float destBase = ref this.destArea.GetReferenceToOrigin();
            int destStride = this.destArea.Stride;

            ref Block8x8F src = ref this.block;

            WidenCopyImpl2x2_V2(ref src, ref destBase, 0, destStride);
            WidenCopyImpl2x2_V2(ref src, ref destBase, 1, destStride);
            WidenCopyImpl2x2_V2(ref src, ref destBase, 2, destStride);
            WidenCopyImpl2x2_V2(ref src, ref destBase, 3, destStride);
            WidenCopyImpl2x2_V2(ref src, ref destBase, 4, destStride);
            WidenCopyImpl2x2_V2(ref src, ref destBase, 5, destStride);
            WidenCopyImpl2x2_V2(ref src, ref destBase, 6, destStride);
            WidenCopyImpl2x2_V2(ref src, ref destBase, 7, destStride);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WidenCopyImpl2x2_V2(ref Block8x8F src, ref float destBase, int row, int destStride)
        {
            ref Vector4 selfLeft = ref Unsafe.Add(ref src.V0L, 2 * row);
            ref Vector4 selfRight = ref Unsafe.Add(ref selfLeft, 1);
            ref float dest0 = ref Unsafe.Add(ref destBase, row * 2 * destStride);

            Unsafe.Add(ref dest0, 0) = selfLeft.X;
            Unsafe.Add(ref dest0, 1) = selfLeft.X;
            Unsafe.Add(ref dest0, 2) = selfLeft.Y;
            Unsafe.Add(ref dest0, 3) = selfLeft.Y;
            Unsafe.Add(ref dest0, 4) = selfLeft.Z;
            Unsafe.Add(ref dest0, 5) = selfLeft.Z;
            Unsafe.Add(ref dest0, 6) = selfLeft.W;
            Unsafe.Add(ref dest0, 7) = selfLeft.W;

            ref float dest1 = ref Unsafe.Add(ref dest0, 8);

            Unsafe.Add(ref dest1, 0) = selfRight.X;
            Unsafe.Add(ref dest1, 1) = selfRight.X;
            Unsafe.Add(ref dest1, 2) = selfRight.Y;
            Unsafe.Add(ref dest1, 3) = selfRight.Y;
            Unsafe.Add(ref dest1, 4) = selfRight.Z;
            Unsafe.Add(ref dest1, 5) = selfRight.Z;
            Unsafe.Add(ref dest1, 6) = selfRight.W;
            Unsafe.Add(ref dest1, 7) = selfRight.W;

            ref float dest2 = ref Unsafe.Add(ref dest0, destStride);

            Unsafe.Add(ref dest2, 0) = selfLeft.X;
            Unsafe.Add(ref dest2, 1) = selfLeft.X;
            Unsafe.Add(ref dest2, 2) = selfLeft.Y;
            Unsafe.Add(ref dest2, 3) = selfLeft.Y;
            Unsafe.Add(ref dest2, 4) = selfLeft.Z;
            Unsafe.Add(ref dest2, 5) = selfLeft.Z;
            Unsafe.Add(ref dest2, 6) = selfLeft.W;
            Unsafe.Add(ref dest2, 7) = selfLeft.W;

            ref float dest3 = ref Unsafe.Add(ref dest2, 8);

            Unsafe.Add(ref dest3, 0) = selfRight.X;
            Unsafe.Add(ref dest3, 1) = selfRight.X;
            Unsafe.Add(ref dest3, 2) = selfRight.Y;
            Unsafe.Add(ref dest3, 3) = selfRight.Y;
            Unsafe.Add(ref dest3, 4) = selfRight.Z;
            Unsafe.Add(ref dest3, 5) = selfRight.Z;
            Unsafe.Add(ref dest3, 6) = selfRight.W;
            Unsafe.Add(ref dest3, 7) = selfRight.W;
        }

        [Benchmark]
        public void UseVector2()
        {
            ref Vector2 destBase = ref Unsafe.As<float, Vector2>(ref this.destArea.GetReferenceToOrigin());
            int destStride = this.destArea.Stride / 2;

            ref Block8x8F src = ref this.block;

            WidenCopyImpl2x2_Vector2(ref src, ref destBase, 0, destStride);
            WidenCopyImpl2x2_Vector2(ref src, ref destBase, 1, destStride);
            WidenCopyImpl2x2_Vector2(ref src, ref destBase, 2, destStride);
            WidenCopyImpl2x2_Vector2(ref src, ref destBase, 3, destStride);
            WidenCopyImpl2x2_Vector2(ref src, ref destBase, 4, destStride);
            WidenCopyImpl2x2_Vector2(ref src, ref destBase, 5, destStride);
            WidenCopyImpl2x2_Vector2(ref src, ref destBase, 6, destStride);
            WidenCopyImpl2x2_Vector2(ref src, ref destBase, 7, destStride);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WidenCopyImpl2x2_Vector2(ref Block8x8F src, ref Vector2 destBase, int row, int destStride)
        {
            ref Vector4 sLeft = ref Unsafe.Add(ref src.V0L, 2 * row);
            ref Vector4 sRight = ref Unsafe.Add(ref sLeft, 1);

            ref Vector2 dTopLeft = ref Unsafe.Add(ref destBase, 2 * row * destStride);
            ref Vector2 dTopRight = ref Unsafe.Add(ref dTopLeft, 4);
            ref Vector2 dBottomLeft = ref Unsafe.Add(ref dTopLeft, destStride);
            ref Vector2 dBottomRight = ref Unsafe.Add(ref dBottomLeft, 4);

            var xLeft = new Vector2(sLeft.X);
            var yLeft = new Vector2(sLeft.Y);
            var zLeft = new Vector2(sLeft.Z);
            var wLeft = new Vector2(sLeft.W);

            var xRight = new Vector2(sRight.X);
            var yRight = new Vector2(sRight.Y);
            var zRight = new Vector2(sRight.Z);
            var wRight = new Vector2(sRight.W);

            dTopLeft = xLeft;
            Unsafe.Add(ref dTopLeft, 1) = yLeft;
            Unsafe.Add(ref dTopLeft, 2) = zLeft;
            Unsafe.Add(ref dTopLeft, 3) = wLeft;

            dTopRight = xRight;
            Unsafe.Add(ref dTopRight, 1) = yRight;
            Unsafe.Add(ref dTopRight, 2) = zRight;
            Unsafe.Add(ref dTopRight, 3) = wRight;

            dBottomLeft = xLeft;
            Unsafe.Add(ref dBottomLeft, 1) = yLeft;
            Unsafe.Add(ref dBottomLeft, 2) = zLeft;
            Unsafe.Add(ref dBottomLeft, 3) = wLeft;

            dBottomRight = xRight;
            Unsafe.Add(ref dBottomRight, 1) = yRight;
            Unsafe.Add(ref dBottomRight, 2) = zRight;
            Unsafe.Add(ref dBottomRight, 3) = wRight;
        }

        [Benchmark]
        public void UseVector4()
        {
            ref Vector2 destBase = ref Unsafe.As<float, Vector2>(ref this.destArea.GetReferenceToOrigin());
            int destStride = this.destArea.Stride / 2;

            ref Block8x8F src = ref this.block;

            WidenCopyImpl2x2_Vector4(ref src, ref destBase, 0, destStride);
            WidenCopyImpl2x2_Vector4(ref src, ref destBase, 1, destStride);
            WidenCopyImpl2x2_Vector4(ref src, ref destBase, 2, destStride);
            WidenCopyImpl2x2_Vector4(ref src, ref destBase, 3, destStride);
            WidenCopyImpl2x2_Vector4(ref src, ref destBase, 4, destStride);
            WidenCopyImpl2x2_Vector4(ref src, ref destBase, 5, destStride);
            WidenCopyImpl2x2_Vector4(ref src, ref destBase, 6, destStride);
            WidenCopyImpl2x2_Vector4(ref src, ref destBase, 7, destStride);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WidenCopyImpl2x2_Vector4(ref Block8x8F src, ref Vector2 destBase, int row, int destStride)
        {
            ref Vector4 sLeft = ref Unsafe.Add(ref src.V0L, 2 * row);
            ref Vector4 sRight = ref Unsafe.Add(ref sLeft, 1);

            ref Vector2 dTopLeft = ref Unsafe.Add(ref destBase, 2 * row * destStride);
            ref Vector2 dTopRight = ref Unsafe.Add(ref dTopLeft, 4);
            ref Vector2 dBottomLeft = ref Unsafe.Add(ref dTopLeft, destStride);
            ref Vector2 dBottomRight = ref Unsafe.Add(ref dBottomLeft, 4);

            var xLeft = new Vector4(sLeft.X);
            var yLeft = new Vector4(sLeft.Y);
            var zLeft = new Vector4(sLeft.Z);
            var wLeft = new Vector4(sLeft.W);

            var xRight = new Vector4(sRight.X);
            var yRight = new Vector4(sRight.Y);
            var zRight = new Vector4(sRight.Z);
            var wRight = new Vector4(sRight.W);

            Unsafe.As<Vector2, Vector4>(ref dTopLeft) = xLeft;
            Unsafe.As<Vector2, Vector4>(ref Unsafe.Add(ref dTopLeft, 1)) = yLeft;
            Unsafe.As<Vector2, Vector4>(ref Unsafe.Add(ref dTopLeft, 2)) = zLeft;
            Unsafe.As<Vector2, Vector4>(ref Unsafe.Add(ref dTopLeft, 3)) = wLeft;

            Unsafe.As<Vector2, Vector4>(ref dTopRight) = xRight;
            Unsafe.As<Vector2, Vector4>(ref Unsafe.Add(ref dTopRight, 1)) = yRight;
            Unsafe.As<Vector2, Vector4>(ref Unsafe.Add(ref dTopRight, 2)) = zRight;
            Unsafe.As<Vector2, Vector4>(ref Unsafe.Add(ref dTopRight, 3)) = wRight;

            Unsafe.As<Vector2, Vector4>(ref dBottomLeft) = xLeft;
            Unsafe.As<Vector2, Vector4>(ref Unsafe.Add(ref dBottomLeft, 1)) = yLeft;
            Unsafe.As<Vector2, Vector4>(ref Unsafe.Add(ref dBottomLeft, 2)) = zLeft;
            Unsafe.As<Vector2, Vector4>(ref Unsafe.Add(ref dBottomLeft, 3)) = wLeft;

            Unsafe.As<Vector2, Vector4>(ref dBottomRight) = xRight;
            Unsafe.As<Vector2, Vector4>(ref Unsafe.Add(ref dBottomRight, 1)) = yRight;
            Unsafe.As<Vector2, Vector4>(ref Unsafe.Add(ref dBottomRight, 2)) = zRight;
            Unsafe.As<Vector2, Vector4>(ref Unsafe.Add(ref dBottomRight, 3)) = wRight;
        }

        // RESULTS:
        //       Method |     Mean |     Error |    StdDev | Scaled |
        // ------------ |---------:|----------:|----------:|-------:|
        //     Original | 88.93 ns | 0.7783 ns | 0.6899 ns |   1.00 |
        //  Original_V2 | 88.39 ns | 0.9426 ns | 0.8356 ns |   0.99 |
        //   UseVector2 | 45.63 ns | 0.4248 ns | 0.3548 ns |   0.51 |
    }
}