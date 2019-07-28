using System.Numerics;
using BenchmarkDotNet.Attributes;

namespace ImageSharp.Benchmarks.General.Vectorization
{
    public class MulUInt32 : SIMDBenchmarkBase<uint>.Multiply
    {
        protected override uint GetTestValue() => 42u;

        [Benchmark(Baseline = true)]
        public void Standard()
        {
            uint v = this.testValue;
            for (int i = 0; i < this.input.Length; i++)
            {
                this.result[i] = this.input[i] * v;
            }
        }
    }

    public class MulInt32 : SIMDBenchmarkBase<int>.Multiply
    {
        [Benchmark(Baseline = true)]
        public void Standard()
        {
            int v = this.testValue;
            for (int i = 0; i < this.input.Length; i++)
            {
                this.result[i] = this.input[i] * v;
            }
        }
    }

    public class MulInt16 : SIMDBenchmarkBase<short>.Multiply
    {
        protected override short GetTestValue() => 42;

        protected override Vector<short> GetTestVector() => new Vector<short>(new short[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17 });

        [Benchmark(Baseline = true)]
        public void Standard()
        {
            short v = this.testValue;
            for (int i = 0; i < this.input.Length; i++)
            {
                this.result[i] = (short)(this.input[i] * v);
            }
        }
    }
}