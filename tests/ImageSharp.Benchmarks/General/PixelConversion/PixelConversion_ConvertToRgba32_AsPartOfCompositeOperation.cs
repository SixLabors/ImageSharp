// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.General.PixelConversion
{
    public class PixelConversion_ConvertToRgba32_AsPartOfCompositeOperation
    {
        private struct ConversionRunner<T>
            where T : struct, ITestPixel<T>
        {
            private T[] source;

            private Rgba32[] dest;

            public ConversionRunner(int count)
            {
                this.source = new T[count];
                this.dest = new Rgba32[count];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RunRetvalConversion()
            {
                int count = this.source.Length;

                ref T sourceBaseRef = ref this.source[0];
                ref Rgba32 destBaseRef = ref this.dest[0];

                Rgba32 temp;

                for (int i = 0; i < count; i++)
                {
                    temp = Unsafe.Add(ref sourceBaseRef, i).ToRgba32();

                    // manipulate pixel before saving to dest buffer:
                    temp.A = 0;

                    Unsafe.Add(ref destBaseRef, i) = temp;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RunCopyToConversion()
            {
                int count = this.source.Length;

                ref T sourceBaseRef = ref this.source[0];
                ref Rgba32 destBaseRef = ref this.dest[0];

                Rgba32 temp = default;

                for (int i = 0; i < count; i++)
                {
                    Unsafe.Add(ref sourceBaseRef, i).CopyToRgba32(ref temp);

                    // manipulate pixel before saving to dest buffer:
                    temp.A = 0;

                    Unsafe.Add(ref destBaseRef, i) = temp;
                }
            }
        }

        private ConversionRunner<TestRgba> compatibleMemoryLayoutRunner;

        private ConversionRunner<TestArgb> permutedRunner;

        [Params(32)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.compatibleMemoryLayoutRunner = new ConversionRunner<TestRgba>(this.Count);
            this.permutedRunner = new ConversionRunner<TestArgb>(this.Count);
        }

        [Benchmark(Baseline = true)]
        public void CompatibleRetval()
        {
            this.compatibleMemoryLayoutRunner.RunRetvalConversion();
        }

        [Benchmark]
        public void CompatibleCopyTo()
        {
            this.compatibleMemoryLayoutRunner.RunCopyToConversion();
        }

        [Benchmark]
        public void PermutedRetval()
        {
            this.permutedRunner.RunRetvalConversion();
        }

        [Benchmark]
        public void PermutedCopyTo()
        {
            this.permutedRunner.RunCopyToConversion();
        }
    }

    // RESULTS:
    //            Method | Count |      Mean |     Error |    StdDev | Scaled | ScaledSD |
    // ----------------- |------ |----------:|----------:|----------:|-------:|---------:|
    //  CompatibleRetval |    32 |  53.05 ns | 0.1865 ns | 0.1557 ns |   1.00 |     0.00 |
    //  CompatibleCopyTo |    32 |  36.12 ns | 0.3596 ns | 0.3003 ns |   0.68 |     0.01 |
    //    PermutedRetval |    32 | 303.61 ns | 5.1697 ns | 4.8358 ns |   5.72 |     0.09 |
    //    PermutedCopyTo |    32 |  38.05 ns | 0.8053 ns | 1.2297 ns |   0.72 |     0.02 |
}
