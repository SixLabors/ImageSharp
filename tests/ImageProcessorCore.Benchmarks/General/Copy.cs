using System;
using System.Runtime.CompilerServices;

namespace ImageProcessorCore.Benchmarks.General
{
    using BenchmarkDotNet.Attributes;

    public class Copy
    {
        private double[] source = new double[10000];

        [Benchmark(Baseline = true, Description = "Copy using Array.Copy()")]
        public double CopyArray()
        {

            double[] destination = new double[10000];
            Array.Copy(source, destination, 10000);

            return destination[0];
        }

        [Benchmark(Description = "Copy using Unsafe<T>")]
        public unsafe double CopyUnsafe()
        {
            double[] destination = new double[10000];
            Unsafe.Copy(Unsafe.AsPointer(ref destination), ref source);

            return destination[0];
        }
    }
}
