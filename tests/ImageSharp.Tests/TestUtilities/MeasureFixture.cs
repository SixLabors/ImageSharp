namespace ImageSharp.Tests
{
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;

    using Xunit.Abstractions;

    /// <summary>
    /// Utility class to measure the execution of an operation. It can be used either by inheritance or by composition.
    /// </summary>
    public class MeasureFixture : TestBase
    {
        /// <summary>
        /// Value indicating whether priniting is enabled.
        /// </summary>
        protected bool EnablePrinting = true;

        /// <summary>
        /// Measures and prints the execution time of an <see cref="Action{T}"/>, executed multiple times.
        /// </summary>
        /// <param name="times">A value indicating how many times to run the action</param>
        /// <param name="action">The <see cref="Action{T}"/> to execute</param>
        /// <param name="operationName">The name of the operation to print to the output</param>
        public void Measure(int times, Action action, [CallerMemberName] string operationName = null)
        {
            if (this.EnablePrinting) this.Output?.WriteLine($"{operationName} X {times} ...");
            Stopwatch sw = Stopwatch.StartNew();

            for (int i = 0; i < times; i++)
            {
                action();
            }

            sw.Stop();
            if (this.EnablePrinting) this.Output?.WriteLine($"{operationName} finished in {sw.ElapsedMilliseconds} ms");
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
}