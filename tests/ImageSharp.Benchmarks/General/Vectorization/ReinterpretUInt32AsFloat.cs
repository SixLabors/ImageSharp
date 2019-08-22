using System.Numerics;
using System.Runtime.InteropServices;

using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.General.Vectorization
{
    public class ReinterpretUInt32AsFloat
    {
        private uint[] input;

        private float[] result;

        [Params(32)]
        public int InputSize { get; set; }

        [StructLayout(LayoutKind.Explicit)]
        struct UIntFloatUnion
        {
            [FieldOffset(0)]
            public float f;

            [FieldOffset(0)]
            public uint i;
        }


        [GlobalSetup]
        public void Setup()
        {
            this.input = new uint[this.InputSize];
            this.result = new float[this.InputSize];
            
            for (int i = 0; i < this.InputSize; i++)
            {
                this.input[i] = (uint)i;
            }
        }

        [Benchmark(Baseline = true)]
        public void Standard()
        {
            UIntFloatUnion u = default;
            for (int i = 0; i < this.input.Length; i++)
            {
                u.i = this.input[i];
                this.result[i] = u.f;
            }
        }

        [Benchmark]
        public void Simd()
        {
            for (int i = 0; i < this.input.Length; i += Vector<uint>.Count)
            {
                var a = new Vector<uint>(this.input, i);
                Vector<float> b = Vector.AsVectorSingle(a);
                b.CopyTo(this.result, i);
            }
        }
    }
}
