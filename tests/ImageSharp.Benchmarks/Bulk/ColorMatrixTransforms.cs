// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Benchmarks.Bulk;

public class ColorMatrixTransforms
{
    private static readonly Vector4[] Vectors = Vector4Factory.CreateVectors();

    [Benchmark(Baseline = true)]
    public void Transform()
    {
        ColorMatrix matrix = KnownFilterMatrices.CreateHueFilter(45F);
        for (int i = 0; i < Vectors.Length; i++)
        {
            ref Vector4 input = ref Vectors[i];
            ColorNumerics.Transform(ref input, ref matrix);
        }
    }

    [Benchmark]
    public void Transform_Span()
    {
        ColorMatrix matrix = KnownFilterMatrices.CreateHueFilter(45F);
        ColorNumerics.Transform(Vectors.AsSpan(), ref matrix);
    }
}
