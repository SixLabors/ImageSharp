// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Benchmarks.Bulk;

public abstract class ToVector4<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    protected IMemoryOwner<TPixel> Source { get; set; }

    protected IMemoryOwner<Vector4> Destination { get; set; }

    protected Configuration Configuration => Configuration.Default;

    [Params(64, 256, 2048)] // 512, 1024
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        this.Source = this.Configuration.MemoryAllocator.Allocate<TPixel>(this.Count);
        this.Destination = this.Configuration.MemoryAllocator.Allocate<Vector4>(this.Count);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.Source.Dispose();
        this.Destination.Dispose();
    }

    // [Benchmark]
    public void Naive()
    {
        Span<TPixel> s = this.Source.GetSpan();
        Span<Vector4> d = this.Destination.GetSpan();

        for (int i = 0; i < this.Count; i++)
        {
            d[i] = s[i].ToVector4();
        }
    }

    [Benchmark]
    public void PixelOperations_Specialized() => PixelOperations<TPixel>.Instance.ToVector4(
            this.Configuration,
            this.Source.GetSpan(),
            this.Destination.GetSpan());
}
