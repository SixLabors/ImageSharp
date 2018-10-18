// ReSharper disable InconsistentNaming

using System.Runtime.CompilerServices;

using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.General.PixelConversion
{
    public class PixelConversion_ConvertFromRgba32
    {
        struct ConversionRunner<T>
            where T : struct, ITestPixel<T>
        {
            private T[] dest;

            private Rgba32[] source;

            public ConversionRunner(int count)
            {
                this.dest = new T[count];
                this.source = new Rgba32[count];
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RunByRefConversion()
            {
                int count = this.dest.Length;

                ref T destBaseRef = ref this.dest[0];
                ref Rgba32 sourceBaseRef = ref this.source[0];

                for (int i = 0; i < count; i++)
                {
                    Unsafe.Add(ref destBaseRef, i).FromRgba32(ref Unsafe.Add(ref sourceBaseRef, i));
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RunByValConversion()
            {
                int count = this.dest.Length;

                ref T destBaseRef = ref this.dest[0];
                ref Rgba32 sourceBaseRef = ref this.source[0];

                for (int i = 0; i < count; i++)
                {
                    Unsafe.Add(ref destBaseRef, i).FromRgba32(Unsafe.Add(ref sourceBaseRef, i));
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void RunFromBytesConversion()
            {
                int count = this.dest.Length;

                ref T destBaseRef = ref this.dest[0];
                ref Rgba32 sourceBaseRef = ref this.source[0];

                for (int i = 0; i < count; i++)
                {
                    ref Rgba32 s = ref Unsafe.Add(ref sourceBaseRef, i);
                    Unsafe.Add(ref destBaseRef, i).FromBytes(s.R, s.G, s.B, s.A);
                }
            }
        }
        
        private ConversionRunner<TestRgba> compatibleMemLayoutRunner;

        private ConversionRunner<TestArgb> permutedRunner;

        [Params(32)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.compatibleMemLayoutRunner = new ConversionRunner<TestRgba>(this.Count);
            this.permutedRunner = new ConversionRunner<TestArgb>(this.Count);
        }

        [Benchmark(Baseline = true)]
        public void CompatibleByRef()
        {
            this.compatibleMemLayoutRunner.RunByRefConversion();
        }

        [Benchmark]
        public void CompatibleByVal()
        {
            this.compatibleMemLayoutRunner.RunByValConversion();
        }

        [Benchmark]
        public void CompatibleFromBytes()
        {
            this.compatibleMemLayoutRunner.RunFromBytesConversion();
        }


        [Benchmark]
        public void PermutedByRef()
        {
            this.permutedRunner.RunByRefConversion();
        }

        [Benchmark]
        public void PermutedByVal()
        {
            this.permutedRunner.RunByValConversion();
        }

        [Benchmark]
        public void PermutedFromBytes()
        {
            this.permutedRunner.RunFromBytesConversion();
        }
    }

    /*
     * Results:
     *              Method | Count |       Mean |    StdDev | Scaled | Scaled-StdDev |
     *  ------------------ |------ |----------- |---------- |------- |-------------- |
     *     CompatibleByRef |    32 | 20.6339 ns | 0.0742 ns |   1.00 |          0.00 |
     *     CompatibleByVal |    32 | 23.7425 ns | 0.0997 ns |   1.15 |          0.01 |
     * CompatibleFromBytes |    32 | 38.7017 ns | 0.1103 ns |   1.88 |          0.01 |
     *       PermutedByRef |    32 | 39.2892 ns | 0.1366 ns |   1.90 |          0.01 |
     *       PermutedByVal |    32 | 38.5178 ns | 0.1946 ns |   1.87 |          0.01 |
     *   PermutedFromBytes |    32 | 38.6683 ns | 0.0801 ns |   1.87 |          0.01 |
     *  
     *  !!! Conclusion !!!
     *  All memory-incompatible (permuted) variants are equivalent with the the "FromBytes" solution. 
     *  In memory compatible cases we should use the optimized Bulk-copying variant anyways, 
     *  so there is no benefit introducing non-bulk API-s other than PackFromBytes() OR PackFromRgba32().
     */
}