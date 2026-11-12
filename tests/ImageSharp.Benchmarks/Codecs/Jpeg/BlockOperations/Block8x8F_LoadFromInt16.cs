// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.Formats.Jpeg.Components;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg.BlockOperations;

public class Block8x8F_LoadFromInt16
{
    private Block8x8 source;
    private Block8x8F destination;

    [GlobalSetup]
    public void Setup()
    {
        for (short i = 0; i < Block8x8F.Size; i++)
        {
            this.source[i] = i;
        }
    }

    [Benchmark]
    public void LoadFrom() => this.destination.LoadFrom(ref this.source);
}
