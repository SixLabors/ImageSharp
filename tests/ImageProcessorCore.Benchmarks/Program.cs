namespace ImageProcessorCore.Benchmarks
{
    using System;
    using System.Reflection;

    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Running;
    using System.Linq;

    public class Program
    {
        public static void Main(string[] args)
        {
            // Use reflection for a more maintainable way of creating the benchmark switcher,
            Type[] benchmarks = typeof(Program).Assembly.GetTypes()
                .Where(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                                .Any(m => m.GetCustomAttributes(typeof(BenchmarkAttribute), false).Any()))
                .OrderBy(t => t.Namespace)
                .ThenBy(t => t.Name)
                .ToArray();

            BenchmarkSwitcher benchmarkSwitcher = new BenchmarkSwitcher(benchmarks);
            benchmarkSwitcher.Run(args);
        }
    }
}
