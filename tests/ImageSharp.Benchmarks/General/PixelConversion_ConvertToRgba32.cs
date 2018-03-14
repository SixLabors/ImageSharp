// ReSharper disable InconsistentNaming

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.General
{
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    using BenchmarkDotNet.Attributes;

    /// <summary>
    /// When implementing TPixel --> Rgba32 style conversions on IPixel, should which API should we prefer?
    /// 1. Rgba32 ToRgba32(); 
    /// OR
    /// 2. void CopyToRgba32(ref Rgba32 dest);
    /// ?
    /// </summary>
    public class PixelConversion_ConvertToRgba32
    {
        interface ITestPixel<T>
            where T : struct, ITestPixel<T>
        {
            Rgba32 ToRgba32();

            void CopyToRgba32(ref Rgba32 dest);
        }

        [StructLayout(LayoutKind.Sequential)]
        struct TestArgb : ITestPixel<TestArgb>
        {
            private byte a, r, g, b;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Rgba32 ToRgba32()
            {
                return new Rgba32(this.r, this.g, this.b, this.a);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void CopyToRgba32(ref Rgba32 dest)
            {
                dest.R = this.r;
                dest.G = this.g;
                dest.B = this.b;
                dest.A = this.a;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct TestRgba : ITestPixel<TestRgba>
        {
            private byte r, g, b, a;
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Rgba32 ToRgba32()
            {
                return Unsafe.As<TestRgba, Rgba32>(ref this);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void CopyToRgba32(ref Rgba32 dest)
            {
                dest = Unsafe.As<TestRgba, Rgba32>(ref this);
            }
        }

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

                for (int i = 0; i < count; i++)
                {
                    Unsafe.Add(ref destBaseRef, i) = Unsafe.Add(ref sourceBaseRef, i).ToRgba32();
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RunCopyToConversion()
            {
                int count = this.source.Length;

                ref T sourceBaseRef = ref this.source[0];
                ref Rgba32 destBaseRef = ref this.dest[0];

                for (int i = 0; i < count; i++)
                {
                    Unsafe.Add(ref sourceBaseRef, i).CopyToRgba32(ref Unsafe.Add(ref destBaseRef, i));
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

    /*
     * Results:
     * 
     *            Method | Count |        Mean |    StdDev | Scaled | Scaled-StdDev |
     *   --------------- |------ |------------ |---------- |------- |-------------- |
     *  CompatibleRetval |   128 |  89.7358 ns | 2.2389 ns |   1.00 |          0.00 |
     *  CompatibleCopyTo |   128 |  89.4112 ns | 2.2901 ns |   1.00 |          0.03 |
     *    PermutedRetval |   128 | 845.4038 ns | 5.6154 ns |   9.43 |          0.23 |
     *    PermutedCopyTo |   128 | 155.6004 ns | 3.8870 ns |   1.73 |          0.06 |
     */
}