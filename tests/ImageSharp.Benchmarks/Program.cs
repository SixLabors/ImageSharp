// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Reflection;

using BenchmarkDotNet.Running;

namespace SixLabors.ImageSharp.Benchmarks
{
    public class Program
    {
        /// <summary>
        /// The main.
        /// </summary>
        /// <param name="args">
        /// The arguments to pass to the program.
        /// </param>
        public static void Main(string[] args)
        {
            new BenchmarkSwitcher(typeof(Program).GetTypeInfo().Assembly).Run(args);
        }
    }
}
