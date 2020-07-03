// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Xunit.Abstractions;

namespace SixLabors.ImageSharp.Tests
{
    /// <summary>
    /// Utility class to measure the execution of an operation. It can be used either by inheritance or by composition.
    /// </summary>
    public class MeasureFixture
    {
        /// <summary>
        /// Value indicating whether printing is enabled.
        /// </summary>
        protected bool enablePrinting = true;

        /// <summary>
        /// Measures and prints the execution time of an <see cref="Action{T}"/>, executed multiple times.
        /// </summary>
        /// <param name="times">A value indicating how many times to run the action</param>
        /// <param name="action">The <see cref="Action{T}"/> to execute</param>
        /// <param name="operationName">The name of the operation to print to the output</param>
        public void Measure(int times, Action action, [CallerMemberName] string operationName = null)
        {
            if (this.enablePrinting)
            {
                this.Output?.WriteLine($"{operationName} X {times} ...");
            }

            var sw = Stopwatch.StartNew();

            for (int i = 0; i < times; i++)
            {
                action();
            }

            sw.Stop();
            if (this.enablePrinting)
            {
                this.Output?.WriteLine($"{operationName} finished in {sw.ElapsedMilliseconds} ms");
            }
        }

        /// <summary>
        /// Initializes a new instance of <see cref="MeasureFixture"/>
        /// </summary>
        /// <param name="output">A <see cref="ITestOutputHelper"/> instance to print the results </param>
        public MeasureFixture(ITestOutputHelper output)
        {
            this.Output = output;
        }

        protected ITestOutputHelper Output { get; }
    }

    public class MeasureGuard : IDisposable
    {
        private readonly string operation;

        private readonly Stopwatch stopwatch = new Stopwatch();

        public MeasureGuard(ITestOutputHelper output, string operation)
        {
            this.operation = operation;
            this.Output = output;
            this.Output.WriteLine(operation + " ...");
            this.stopwatch.Start();
        }

        private ITestOutputHelper Output { get; }

        public void Dispose()
        {
            this.stopwatch.Stop();
            this.Output.WriteLine($"{this.operation} completed in {this.stopwatch.ElapsedMilliseconds}ms");
        }
    }
}
