// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Numerics;
using System.Runtime.CompilerServices;

using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.General.PixelConversion
{
    public class PixelConversion_ConvertToVector4_AsPartOfCompositeOperation
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

                Vector4 temp;

                for (int i = 0; i < count; i++)
                {
                    temp = Unsafe.Add(ref sourceBaseRef, i).ToVector4();

                    // manipulate pixel before saving to dest buffer:
                    temp.W = 0;

                    Unsafe.Add(ref destBaseRef, i) = temp;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RunCopyToConversion()
            {
                int count = this.source.Length;

                ref T sourceBaseRef = ref this.source[0];
                ref Vector4 destBaseRef = ref this.dest[0];

                Vector4 temp = default;

                for (int i = 0; i < count; i++)
                {
                    Unsafe.Add(ref sourceBaseRef, i).CopyToVector4(ref temp);

                    // manipulate pixel before saving to dest buffer:
                    temp.W = 0;

                    Unsafe.Add(ref destBaseRef, i) = temp;
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
        //     Method | Count |     Mean |    Error |   StdDev | Scaled | ScaledSD |
        // ---------- |------ |---------:|---------:|---------:|-------:|---------:|
        //  UseRetval |    32 | 120.2 ns | 1.560 ns | 1.383 ns |   1.00 |     0.00 |
        //  UseCopyTo |    32 | 121.7 ns | 2.439 ns | 2.281 ns |   1.01 |     0.02 |
    }
}
