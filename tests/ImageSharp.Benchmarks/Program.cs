// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Running;

namespace SixLabors.ImageSharp.Benchmarks;

public class Program
{
    /// <summary>
    /// The main.
    /// </summary>
    /// <param name="args">
    /// The arguments to pass to the program.
    /// </param>
    public static void Main(string[] args) => BenchmarkSwitcher
        .FromAssembly(typeof(Program).Assembly)
        .Run(args);
}
