using System.Runtime.CompilerServices;

using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.General.PixelConversion
{
    public class PixelConversion_ConvertToRgba32_AsPartOfCompositeOperation
    {
        struct ConversionRunner<T>
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

        [Params(128)]
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
    //            Method | Count |       Mean |     Error |    StdDev | Scaled | ScaledSD |
    // ----------------- |------ |-----------:|----------:|----------:|-------:|---------:|
    //  CompatibleRetval |   128 |   210.1 ns | 0.8443 ns | 0.7484 ns |   1.00 |     0.00 |
    //  CompatibleCopyTo |   128 |   140.1 ns | 0.4297 ns | 0.4019 ns |   0.67 |     0.00 |
    //    PermutedRetval |   128 | 1,044.6 ns | 3.7901 ns | 3.3599 ns |   4.97 |     0.02 |
    //    PermutedCopyTo |   128 |   140.3 ns | 0.6495 ns | 0.5757 ns |   0.67 |     0.00 |
}