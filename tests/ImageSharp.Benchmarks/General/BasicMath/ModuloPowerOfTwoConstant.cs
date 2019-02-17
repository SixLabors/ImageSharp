using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.General.BasicMath
{
    [LongRunJob]
    public class ModuloPowerOfTwoConstant
    {
        private readonly int value = 42;

        [Benchmark(Baseline = true)]
        public int Standard()
        {
            return this.value % 8;
        }

        [Benchmark]
        public int Bitwise()
        {
            return ImageMaths.Modulo8(this.value);
        }
    }
}