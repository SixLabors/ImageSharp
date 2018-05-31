// ReSharper disable InconsistentNaming

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.Formats.Jpeg.Components;

namespace SixLabors.ImageSharp.Benchmarks.General
{
    public class Block8x8F_Round
    {
        private Block8x8F block = default(Block8x8F);

        [GlobalSetup]
        public void Setup()
        {
            if (Vector<float>.Count != 8)
            {
                throw new NotSupportedException("Vector<float>.Count != 8");
            }

            for (int i = 0; i < Block8x8F.Size; i++)
            {
                this.block[i] = i * 44.8f;
            }
        }

        [Benchmark(Baseline = true)]
        public void ScalarRound()
        {
            ref float b = ref Unsafe.As<Block8x8F, float>(ref this.block);

            for (int i = 0; i < Block8x8F.Size; i++)
            {
                ref float v = ref Unsafe.Add(ref b, i);
                v = (float)Math.Round(v);
            }
        }

        [Benchmark]
        public void SimdRound()
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
    }
}