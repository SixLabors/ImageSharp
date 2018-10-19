using System.Numerics;
using System.Runtime.CompilerServices;

using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.General.Vectorization
{
    public class UInt32ToSingle
    {
        private float[] data;

        private const int Count = 64;

        [GlobalSetup]
        public void Setup()
        {
            this.data = new float[Count];
        }

        [Benchmark(Baseline = true)]
        public void MagicMethod()
        {
            ref Vector<float> b = ref Unsafe.As<float, Vector<float>>(ref this.data[0]);

            int n = Count / Vector<float>.Count;

            Vector<float> magick = new Vector<float>(32768.0f);
            Vector<float> scale = new Vector<float>(255f) / new Vector<float>(256f);

            for (int i = 0; i < n; i++)
            {
                // union { float f; uint32_t i; } u;
                // u.f = 32768.0f + x * (255.0f / 256.0f);
                // return (uint8_t)u.i;

                ref Vector<float> d = ref Unsafe.Add(ref b, i);
                Vector<float> x = d;
                //x = Vector.Max(x, Vector<float>.Zero);
                //x = Vector.Min(x, Vector<float>.One);

                x = (x * scale) + magick;
                d = x;
            }
        }

        [Benchmark]
        public void StandardSimd()
        {
            int n = Count / Vector<float>.Count;

            ref Vector<float> b = ref Unsafe.As<float, Vector<float>>(ref this.data[0]);

            var scale = new Vector<float>(1f / 255f);

            for (int i = 0; i < n; i++)
            {
                ref Vector<float> df = ref Unsafe.Add(ref b, i);
                Vector<uint> du = Unsafe.As<Vector<float>, Vector<uint>>(ref df);

                Vector<float> v = Vector.ConvertToSingle(du);
                v *= scale;
                df = v;
            }
        }
    }
}