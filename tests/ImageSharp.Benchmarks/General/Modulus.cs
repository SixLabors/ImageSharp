namespace ImageSharp.Benchmarks.General
{
    using BenchmarkDotNet.Attributes;

    public class Modulus
    {
        [Benchmark(Baseline = true, Description = "Standard Modulus using %")]
        public int StandardModulus()
        {
            return 255 % 256;
        }

        [Benchmark(Description = "Bitwise Modulus using &")]
        public int BitwiseModulus()
        {
            return 255 & 255;
        }
    }
}
