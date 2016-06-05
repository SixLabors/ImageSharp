namespace ImageProcessorCore.Benchmarks
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    using BenchmarkDotNet.Attributes;
    using BenchmarkDotNet.Running;

    public class Program
    {
        /// <summary>
        /// The main.
        /// </summary>
        /// <param name="args">
        /// The arguments to pas to the program.
        /// </param>
        public static void Main(string[] args)
        {
            // Use reflection for a more maintainable way of creating the benchmark switcher,
            Type[] benchmarks = typeof(Program).Assembly.GetTypes()
                .Where(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                             .Any(m => m.GetCustomAttributes(typeof(BenchmarkAttribute), false).Any()))
                .OrderBy(t => t.Namespace)
                .ThenBy(t => t.Name)
                .ToArray();

            // TODO: This throws an exception.
            // List<string> x = new List<string>(args) { "diagnosers=MemoryDiagnoser,InliningDiagnoser" };
            BenchmarkSwitcher benchmarkSwitcher = new BenchmarkSwitcher(benchmarks);
            
            // benchmarkSwitcher.Run(x.ToArray());
            benchmarkSwitcher.Run(args);
        }
    }
}
