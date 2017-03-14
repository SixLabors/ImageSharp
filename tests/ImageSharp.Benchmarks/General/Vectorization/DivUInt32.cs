namespace ImageSharp.Benchmarks.General.Vectorization
{
    using System.Numerics;

    using BenchmarkDotNet.Attributes;

    public class DivUInt32
    {
        private uint[] input;

        private uint[] result;

        [Params(32)]
        public int InputSize { get; set; }

        private uint testValue;

        [Setup]
        public void Setup()
        {
            this.input = new uint[this.InputSize];
            this.result = new uint[this.InputSize];
            this.testValue = 42;

            for (int i = 0; i < this.InputSize; i++)
            {
                this.input[i] = (uint)i;
            }
        }

        [Benchmark(Baseline = true)]
        public void Standard()
        {
            uint v = this.testValue;
            for (int i = 0; i < this.input.Length; i++)
            {
                this.result[i] = this.input[i] / v;
            }
        }

        [Benchmark]
        public void Simd()
        {
            Vector<uint> v = new Vector<uint>(this.testValue);

            for (int i = 0; i < this.input.Length; i += Vector<uint>.Count)
            {
                Vector<uint> a = new Vector<uint>(this.input, i);
                a = a / v;
                a.CopyTo(this.result, i);
            }
        }
    }
}