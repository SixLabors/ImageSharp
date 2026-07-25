// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats.Utils;

namespace SixLabors.ImageSharp.Benchmarks.General.PixelConversion;

/// <summary>
/// Exposes every stateful affine operator and traversal remainder for assembly inspection.
/// </summary>
[Config(typeof(Config.Analysis))]
public class Vector4AffineTransformAssembly
{
    private static readonly Vector4 Multiplier = new(255F, 2F, 65535F, .5F);
    private static readonly Vector4 Offset = new(17F, -1F, 32768F, 3F);
    private static readonly Vector4 Divisor = new(255F, 2F, 65535F, .5F);

    private Vector4[] vectors;

    /// <summary>
    /// Gets or sets the number of vectors transformed by each invocation.
    /// </summary>
    /// <remarks>
    /// Three vectors exercise the 256- and 128-bit stages. Seventeen vectors exercise
    /// the 512-bit loop and leave one vector for the 128-bit remainder.
    /// </remarks>
    [Params(3, 17)]
    public int Count { get; set; }

    /// <summary>
    /// Creates a non-uniform input buffer.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.vectors = new Vector4[this.Count];

        for (int i = 0; i < this.vectors.Length; i++)
        {
            this.vectors[i] = new Vector4(i + .25F, i + .5F, i + .75F, i + 1F);
        }
    }

    /// <summary>
    /// Executes the multiply-then-add stateful operator.
    /// </summary>
    [Benchmark]
    public void MultiplyThenAdd()
        => Vector4Converters.MultiplyThenAdd(this.vectors, Multiplier, Offset);

    /// <summary>
    /// Executes the add-then-divide stateful operator.
    /// </summary>
    [Benchmark]
    public void AddThenDivide()
        => Vector4Converters.AddThenDivide(this.vectors, Offset, Divisor);
}
