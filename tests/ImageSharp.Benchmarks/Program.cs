// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;

namespace SixLabors.ImageSharp.Benchmarks;

public class Program
{
    /// <summary>
    /// Runs benchmarks with a menu that lists benchmark classes in alphabetical order.
    /// </summary>
    /// <param name="args">
    /// The BenchmarkDotNet command-line arguments.
    /// </param>
    public static void Main(string[] args)
    {
        IReadOnlyList<Type> benchmarkTypes = TypeFilter.GetTypesWithRunnableBenchmarks([], [typeof(Program).Assembly], ConsoleLogger.Default).runnable;

        // Discovery expands generic benchmarks. Pass each definition once because the switcher expands
        // its GenericTypeArguments attributes again. Sort before the switcher assigns menu numbers.
        Type[] orderedTypes = benchmarkTypes
            .Select(type => type.IsConstructedGenericType ? type.GetGenericTypeDefinition() : type)
            .Distinct()
            .OrderBy(type => type.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        BenchmarkSwitcher.FromTypes(orderedTypes).Run(args);
    }
}
