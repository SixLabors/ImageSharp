namespace SixLabors.ImageSharp.Benchmarks.General
{
    using System.Numerics;
    using System.Runtime.CompilerServices;

    using BenchmarkDotNet.Attributes;

    using SixLabors.ImageSharp.Memory;

    public class IterateArray
    {
        // Usual pinned stuff
        private Buffer<Vector4> buffer;

        // An array that's not pinned by intent!
        private Vector4[] array;
        
        [Params(64, 1024)]
        public int Length { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.buffer = new Buffer<Vector4>(this.Length);
            this.buffer.Pin();
            this.array = new Vector4[this.Length];
        }

        [Benchmark(Baseline = true)]
        public Vector4 IterateIndexed()
        {
            Vector4 sum = new Vector4();
            Vector4[] a = this.array;

            for (int i = 0; i < a.Length; i++)
            {
                sum += a[i];
            }
            return sum;
        }

        [Benchmark]
        public unsafe Vector4 IterateUsingPointers()
        {
            Vector4 sum = new Vector4();

            Vector4* ptr = (Vector4*) this.buffer.Pin();
            Vector4* end = ptr + this.Length;

            for (; ptr < end; ptr++)
            {
                sum += *ptr;
            }

            return sum;
        }

        [Benchmark]
        public Vector4 IterateUsingReferences()
        {
            Vector4 sum = new Vector4();

            ref Vector4 start = ref this.array[0];

            for (int i = 0; i < this.Length; i++)
            {
                sum += Unsafe.Add(ref start, i);
            }

            return sum;
        }
    }
}