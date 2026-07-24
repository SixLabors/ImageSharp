// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Benchmarks.General.BasicMath;

public class AddSpan
{
    private byte[] scalarValues = null!;
    private byte[] tensorValues = null!;
    private byte[] addends = null!;

    /// <summary>
    /// Gets or sets the number of values to add.
    /// </summary>
    [Params(32, 257, 2048)]
    public int Length { get; set; }

    /// <summary>
    /// Creates equivalent deterministic inputs for both implementations.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.scalarValues = new byte[this.Length];
        this.tensorValues = new byte[this.Length];
        this.addends = new byte[this.Length];

        for (int i = 0; i < this.Length; i++)
        {
            byte value = (byte)((i * 17) + 31);
            this.scalarValues[i] = value;
            this.tensorValues[i] = value;
            this.addends[i] = (byte)((i * 29) + 7);
        }
    }

    /// <summary>
    /// Adds the values with a scalar loop.
    /// </summary>
    /// <returns>The first result, which keeps the mutated data observable to the benchmark harness.</returns>
    [Benchmark(Baseline = true)]
    public byte Scalar()
    {
        for (int i = 0; i < this.scalarValues.Length; i++)
        {
            this.scalarValues[i] += this.addends[i];
        }

        return this.scalarValues[0];
    }

    /// <summary>
    /// Adds the values with the tensor compatibility pipeline.
    /// </summary>
    /// <returns>The first result, which keeps the mutated data observable to the benchmark harness.</returns>
    [Benchmark]
    public byte TensorPipeline()
    {
        TensorPrimitives_.Add<byte>(this.tensorValues, this.addends, this.tensorValues);
        return this.tensorValues[0];
    }
}
