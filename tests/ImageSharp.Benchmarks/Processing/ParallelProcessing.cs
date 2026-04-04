// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests;

namespace SixLabors.ImageSharp.Benchmarks;

public class ParallelProcessing
{
    private Image<Rgba32> image;
    private Configuration configuration;

    public static IEnumerable<int> MaxDegreeOfParallelismValues()
    {
        int processorCount = Environment.ProcessorCount;
        for (int p = 1; p <= processorCount; p *= 2)
        {
            yield return p;
        }

        if ((processorCount & (processorCount - 1)) != 0)
        {
            yield return processorCount;
        }
    }

    [ParamsSource(nameof(MaxDegreeOfParallelismValues))]
    public int MaxDegreeOfParallelism { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.image = new Image<Rgba32>(2048, 2048);
        this.configuration = Configuration.Default.Clone();
        this.configuration.MaxDegreeOfParallelism = this.MaxDegreeOfParallelism;
    }

    [Benchmark]
    public void DetectEdges() => this.image.Mutate(this.configuration, x => x.DetectEdges());

    [Benchmark]
    public void Crop()
    {
        Rectangle bounds = this.image.Bounds;
        bounds = new Rectangle(1, 1, bounds.Width - 2, bounds.Height - 2);
        this.image
            .Clone(this.configuration, x => x.Crop(bounds))
            .Dispose();
    }

    [GlobalCleanup]
    public void Cleanup() => this.image.Dispose();
}
