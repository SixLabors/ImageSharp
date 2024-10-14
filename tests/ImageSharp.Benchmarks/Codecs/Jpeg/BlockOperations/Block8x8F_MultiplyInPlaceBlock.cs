// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg.Components;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg.BlockOperations;

[Config(typeof(Config.HwIntrinsics_SSE_AVX))]
public class Block8x8F_MultiplyInPlaceBlock
{
    private static readonly Block8x8F Source = Create8x8FloatData();

    [Benchmark]
    public void MultiplyInPlaceBlock()
    {
        Block8x8F dest = default;
        Source.MultiplyInPlace(ref dest);
    }

    private static Block8x8F Create8x8FloatData()
    {
        float[] result = new float[64];
        for (int i = 0; i < 8; i++)
        {
            for (int j = 0; j < 8; j++)
            {
                result[(i * 8) + j] = (i * 10) + j;
            }
        }

        return Block8x8F.Load(result);
    }
}
