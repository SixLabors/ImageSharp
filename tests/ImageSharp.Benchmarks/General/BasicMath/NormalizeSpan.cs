// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;

namespace SixLabors.ImageSharp.Benchmarks.General.BasicMath;

public class NormalizeSpan
{
    private float[] scalarValues = null!;
    private float[] tensorValues = null!;

    /// <summary>
    /// Gets or sets the number of values to normalize.
    /// </summary>
    [Params(7, 32, 257, 2048)]
    public int Length { get; set; }

    /// <summary>
    /// Creates equivalent deterministic inputs for both implementations.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.scalarValues = new float[this.Length];
        this.tensorValues = new float[this.Length];

        for (int i = 0; i < this.scalarValues.Length; i++)
        {
            float value = ((i * 17) % 251) + 1;
            this.scalarValues[i] = value;
            this.tensorValues[i] = value;
        }
    }

    /// <summary>
    /// Normalizes the values with a scalar loop.
    /// </summary>
    /// <returns>The first result, which keeps the mutated data observable to the benchmark harness.</returns>
    [Benchmark(Baseline = true)]
    public float Scalar()
    {
        for (int i = 0; i < this.scalarValues.Length; i++)
        {
            this.scalarValues[i] /= 4096F;
        }

        return this.scalarValues[0];
    }

    /// <summary>
    /// Normalizes the values with the tensor compatibility pipeline.
    /// </summary>
    /// <returns>The first result, which keeps the mutated data observable to the benchmark harness.</returns>
    [Benchmark]
    public float TensorPipeline()
    {
        Numerics.Normalize(this.tensorValues, 4096F);
        return this.tensorValues[0];
    }
}
