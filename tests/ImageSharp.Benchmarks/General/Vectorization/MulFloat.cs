namespace ImageSharp.Benchmarks.General.Vectorization
{
    using System.Numerics;

    using BenchmarkDotNet.Attributes;

    public class MulFloat
    {
        private float[] input;

        private float[] result;

        [Params(32)]
        public int InputSize { get; set; }

        private float testValue;

        [GlobalSetup]
        public void Setup()
        {
            this.input = new float[this.InputSize];
            this.result = new float[this.InputSize];
            this.testValue = 42;

            for (int i = 0; i < this.InputSize; i++)
            {
                this.input[i] = i;
            }
        }

        [Benchmark(Baseline = true)]
        public void Standard()
        {
            float v = this.testValue;
            for (int i = 0; i < this.input.Length; i++)
            {
                this.result[i] = this.input[i] * v;
            }
        }

        [Benchmark]
        public void SimdMultiplyByVector()
        {
            Vector<float> v = new Vector<float>(this.testValue);

            for (int i = 0; i < this.input.Length; i += Vector<uint>.Count)
            {
                Vector<float> a = new Vector<float>(this.input, i);
                a = a * v;
                a.CopyTo(this.result, i);
            }
        }

        [Benchmark]
        public void SimdMultiplyByScalar()
        {
            float v = this.testValue;

            for (int i = 0; i < this.input.Length; i += Vector<uint>.Count)
            {
                Vector<float> a = new Vector<float>(this.input, i);
                a = a * v;
                a.CopyTo(this.result, i);
            }
        }
    }
}