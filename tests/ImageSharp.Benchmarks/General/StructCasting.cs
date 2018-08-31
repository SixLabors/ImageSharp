using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.General
{
    public class StructCasting
    {
        [Benchmark(Baseline = true)]
        public short ExplicitCast()
        {
            int x = 5 * 2;
            return (short)x;
        }

        [Benchmark]
        public short UnsafeCast()
        {
            int x = 5 * 2;
            return Unsafe.As<int, short>(ref x);
        }

        [Benchmark]
        public short UnsafeCastRef()
        {
            return Unsafe.As<int, short>(ref Unsafe.AsRef(5 * 2));
        }
    }
}
