// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats.Utils;

namespace SixLabors.ImageSharp.Benchmarks.General.PixelConversion;

/// <summary>
/// Measures the operator-driven affine vector transforms.
/// </summary>
[Config(typeof(Config.Short))]
public class Vector4AffineTransform
{
    private static readonly Vector4 Multiplier = new(255F, 2F, 65535F, .5F);
    private static readonly Vector4 Offset = new(17F, -1F, 32768F, 3F);
    private static readonly Vector4 Divisor = new(255F, 2F, 65535F, .5F);

    private Vector4[] current;

    /// <summary>
    /// Gets or sets the number of vectors transformed by each invocation.
    /// </summary>
    [Params(1, 3, 4, 17, 256, 4096)]
    public int Count { get; set; }

    /// <summary>
    /// Creates a non-uniform input buffer.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.current = new Vector4[this.Count];

        for (int i = 0; i < this.current.Length; i++)
        {
            this.current[i] = new Vector4(i + .25F, i + .5F, i + .75F, i + 1F);
        }
    }

    /// <summary>
    /// Executes the operator-driven multiply-then-add traversal.
    /// </summary>
    [Benchmark]
    public void MultiplyThenAdd()
        => Vector4Converters.MultiplyThenAdd(this.current, Multiplier, Offset);

    /// <summary>
    /// Executes the operator-driven add-then-divide traversal.
    /// </summary>
    [Benchmark]
    public void AddThenDivide()
        => Vector4Converters.AddThenDivide(this.current, Offset, Divisor);
}
