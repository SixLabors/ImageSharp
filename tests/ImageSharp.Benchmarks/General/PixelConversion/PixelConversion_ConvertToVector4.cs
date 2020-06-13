// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;

using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.General.PixelConversion
{
    public class PixelConversion_ConvertToVector4
    {
        private struct ConversionRunner<T>
            where T : struct, ITestPixel<T>
        {
            private T[] source;

            private Vector4[] dest;

            public ConversionRunner(int count)
            {
                this.source = new T[count];
                this.dest = new Vector4[count];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RunRetvalConversion()
            {
                int count = this.source.Length;

                ref T sourceBaseRef = ref this.source[0];
                ref Vector4 destBaseRef = ref this.dest[0];

                for (int i = 0; i < count; i++)
                {
                    Unsafe.Add(ref destBaseRef, i) = Unsafe.Add(ref sourceBaseRef, i).ToVector4();
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RunCopyToConversion()
            {
                int count = this.source.Length;

                ref T sourceBaseRef = ref this.source[0];
                ref Vector4 destBaseRef = ref this.dest[0];

                for (int i = 0; i < count; i++)
                {
                    Unsafe.Add(ref sourceBaseRef, i).CopyToVector4(ref Unsafe.Add(ref destBaseRef, i));
                }
            }
        }

        private ConversionRunner<TestRgba> runner;

        [Params(32)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.runner = new ConversionRunner<TestRgba>(this.Count);
        }

        [Benchmark(Baseline = true)]
        public void UseRetval()
        {
            this.runner.RunRetvalConversion();
        }

        [Benchmark]
        public void UseCopyTo()
        {
            this.runner.RunCopyToConversion();
        }

        // RESULTS:
        //     Method | Count |     Mean |    Error |   StdDev | Scaled |
        // ---------- |------ |---------:|---------:|---------:|-------:|
        //  UseRetval |    32 | 109.0 ns | 1.202 ns | 1.125 ns |   1.00 |
        //  UseCopyTo |    32 | 108.6 ns | 1.151 ns | 1.020 ns |   1.00 |
    }
}
