// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Benchmarks.General.Vectorization;

[Config(typeof(Config.MultiFramework))]
public class ColorNumerics
{
    private static Vector4[] input =
        {
            new(.5F), new(.5F), new(.5F), new(.5F), new(.5F), new(.5F), new(.5F),
        };

    [Benchmark]
    public Vector4 Transform()
    {
        Vector4 input = new(.5F);
        ColorMatrix matrix = KnownFilterMatrices.CreateHueFilter(45F);
        ImageSharp.ColorNumerics.Transform(ref input, ref matrix);

        return input;
    }

    [Benchmark]
    public void Transform_Span()
    {
        ColorMatrix matrix = KnownFilterMatrices.CreateHueFilter(45F);
        ImageSharp.ColorNumerics.Transform(input.AsSpan(), ref matrix);
    }
}
