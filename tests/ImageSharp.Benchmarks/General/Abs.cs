namespace ImageSharp.Benchmarks.General
{
    using System;

    using BenchmarkDotNet.Attributes;

    public class Abs
    {
        [Params(-1, 1)]
        public int X { get; set; }

        [Benchmark(Baseline = true, Description = "Maths Abs")]
        public int MathAbs()
        {
            int x = this.X;
            return Math.Abs(x);
        }

        [Benchmark(Description = "Conditional Abs")]
        public int ConditionalAbs()
        {
            int x = this.X;
            return x < 0 ? -x : x;
        }

        [Benchmark(Description = "Bitwise Abs")]
        public int AbsBitwise()
        {
            int x = this.X;
            return (x ^ (x >> 31)) - (x >> 31);
        }
    }
}
