// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;

using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.Formats.Jpeg.Components;

// ReSharper disable InconsistentNaming

namespace SixLabors.ImageSharp.Benchmarks.General
{
    /// <summary>
    /// The goal of this benchmark is to measure the following Jpeg-related scenario:
    /// - Take 2 blocks of float-s
    /// - Divide each float pair, round the result
    /// - Iterate through all rounded values as int-s
    /// </summary>
    public unsafe class Block8x8F_DivideRound
    {
        private const int ExecutionCount = 5; // Added this to reduce the effect of copying the blocks
        private static readonly Vector4 MinusOne = new Vector4(-1);
        private static readonly Vector4 Half = new Vector4(0.5f);

        private Block8x8F inputDividend = default(Block8x8F);
        private Block8x8F inputDivisior = default(Block8x8F);

        [GlobalSetup]
        public void Setup()
        {
            for (int i = 0; i < Block8x8F.Size; i++)
            {
                this.inputDividend[i] = i*44.8f;
                this.inputDivisior[i] = 100 - i;
            }
        }

        [Benchmark(Baseline = true)]
        public int ByRationalIntegers()
        {
            int sum = 0;

            Block8x8F b1 = this.inputDividend;
            Block8x8F b2 = this.inputDivisior;
            float* pDividend = (float*)&b1;
            float* pDivisor = (float*)&b2;

            int* result = stackalloc int[Block8x8F.Size];

            for (int cnt = 0; cnt < ExecutionCount; cnt++)
            {
                sum = 0;
                for (int i = 0; i < Block8x8F.Size; i++)
                {
                    int a = (int) pDividend[i];
                    int b = (int) pDivisor;
                    result[i] = RationalRound(a, b);
                }
                for (int i = 0; i < Block8x8F.Size; i++)
                {
                    sum += result[i];
                }
            }

            return sum;
        }

        [Benchmark]
        public int BySystemMathRound()
        {
            int sum = 0;

            Block8x8F b1 = this.inputDividend;
            Block8x8F b2 = this.inputDivisior;
            float* pDividend = (float*)&b1;
            float* pDivisor = (float*)&b2;

            for (int cnt = 0; cnt < ExecutionCount; cnt++)
            {
                sum = 0;
                for (int i = 0; i < Block8x8F.Size; i++)
                {
                    double value = pDividend[i] / pDivisor[i];
                    pDividend[i] = (float) System.Math.Round(value);
                }
                for (int i = 0; i < Block8x8F.Size; i++)
                {
                    sum += (int) pDividend[i];
                }
            }
            return sum;
        }

        [Benchmark]
        public int BySimdMagic()
        {
            int sum = 0;

            Block8x8F bDividend = this.inputDividend;
            Block8x8F bDivisor = this.inputDivisior;
            float* pDividend = (float*)&bDividend;

            for (int cnt = 0; cnt < ExecutionCount; cnt++)
            {
                sum = 0;
                DivideRoundAll(ref bDividend, ref bDivisor);
                for (int i = 0; i < Block8x8F.Size; i++)
                {
                    sum += (int)pDividend[i];
                }
            }
            return sum;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void DivideRoundAll(ref Block8x8F a, ref Block8x8F b)
        {
            a.V0L = DivideRound(a.V0L, b.V0L);
            a.V0R = DivideRound(a.V0R, b.V0R);
            a.V1L = DivideRound(a.V1L, b.V1L);
            a.V1R = DivideRound(a.V1R, b.V1R);
            a.V2L = DivideRound(a.V2L, b.V2L);
            a.V2R = DivideRound(a.V2R, b.V2R);
            a.V3L = DivideRound(a.V3L, b.V3L);
            a.V3R = DivideRound(a.V3R, b.V3R);
            a.V4L = DivideRound(a.V4L, b.V4L);
            a.V4R = DivideRound(a.V4R, b.V4R);
            a.V5L = DivideRound(a.V5L, b.V5L);
            a.V5R = DivideRound(a.V5R, b.V5R);
            a.V6L = DivideRound(a.V6L, b.V6L);
            a.V6R = DivideRound(a.V6R, b.V6R);
            a.V7L = DivideRound(a.V7L, b.V7L);
            a.V7R = DivideRound(a.V7R, b.V7R);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector4 DivideRound(Vector4 dividend, Vector4 divisor)
        {
            Vector4 sign = Vector4.Min(dividend, Vector4.One);
            sign = Vector4.Max(sign, MinusOne);

            return dividend / divisor + sign * Half;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int RationalRound(int dividend, int divisor)
        {
            if (dividend >= 0)
            {
                return (dividend + (divisor >> 1)) / divisor;
            }

            return -((-dividend + (divisor >> 1)) / divisor);
        }
    }
}