using System;

namespace ImageProcessorCore.Benchmarks.Color
{
    using BenchmarkDotNet.Attributes;

    public class Clamp
    {
        [Benchmark(Baseline = true, Description = "Maths Clamp")]
        public byte ClampMaths()
        {
            double value = 256;
            return (byte)Math.Min(Math.Max(0, value), 255);
        }

        [Benchmark(Description = "No Maths Clamp")]
        public byte ClampNoMaths()
        {
            double value = 256;
            value = (value > 255) ? 255 : value;
            value = (value < 0) ? 0 : value;
            return (byte)value;
        }

    }
}
