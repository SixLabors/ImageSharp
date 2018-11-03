using System;
using System.Numerics;
using System.Runtime.CompilerServices;

using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Memory;

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
                throw new InvalidOperationException("Block8x8F_CopyTo1x1 is invalid on platforms without AVX2 support.");
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
            ref Vector<float> d = ref Unsafe.As<float, Vector<float>>(ref this.destArea.GetReferenceToOrigin());

            Vector<float> row0 = Unsafe.As<Vector4, Vector<float>>(ref s.V0L);
            Vector<float> row1 = Unsafe.As<Vector4, Vector<float>>(ref s.V1L);
            Vector<float> row2 = Unsafe.As<Vector4, Vector<float>>(ref s.V2L);
            Vector<float> row3 = Unsafe.As<Vector4, Vector<float>>(ref s.V3L);
            Vector<float> row4 = Unsafe.As<Vector4, Vector<float>>(ref s.V4L);
            Vector<float> row5 = Unsafe.As<Vector4, Vector<float>>(ref s.V5L);
            Vector<float> row6 = Unsafe.As<Vector4, Vector<float>>(ref s.V6L);
            Vector<float> row7 = Unsafe.As<Vector4, Vector<float>>(ref s.V7L);

            d = row0;
            Unsafe.Add(ref d, 1) = row1;
            Unsafe.Add(ref d, 2) = row2;
            Unsafe.Add(ref d, 3) = row3;
            Unsafe.Add(ref d, 4) = row4;
            Unsafe.Add(ref d, 5) = row5;
            Unsafe.Add(ref d, 6) = row6;
            Unsafe.Add(ref d, 7) = row7;
        }
    }
}