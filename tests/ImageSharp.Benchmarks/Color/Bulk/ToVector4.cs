// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Benchmarks.ColorSpaces.Bulk
{
    using System;
    using System.Numerics;

    using BenchmarkDotNet.Attributes;

    using SixLabors.Memory;
    using SixLabors.ImageSharp.PixelFormats;

    public abstract class ToVector4<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private IBuffer<TPixel> source;

        private IBuffer<Vector4> destination;

        [Params(64, 300, 1024)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.source = Configuration.Default.MemoryAllocator.Allocate<TPixel>(this.Count);
            this.destination = Configuration.Default.MemoryAllocator.Allocate<Vector4>(this.Count);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.source.Dispose();
            this.destination.Dispose();
        }

        [Benchmark(Baseline = true)]
        public void PerElement()
        {
            Span<TPixel> s = this.source.GetSpan();
            Span<Vector4> d = this.destination.GetSpan();

            for (int i = 0; i < this.Count; i++)
            {
                TPixel c = s[i];
                d[i] = c.ToVector4();
            }
        }

        [Benchmark]
        public void CommonBulk()
        {
            new PixelOperations<TPixel>().ToVector4(this.source.GetSpan(), this.destination.GetSpan(), this.Count);
        }

        [Benchmark]
        public void OptimizedBulk()
        {
            PixelOperations<TPixel>.Instance.ToVector4(this.source.GetSpan(), this.destination.GetSpan(), this.Count);
        }
    }

    public class ToVector4_Rgba32 : ToVector4<Rgba32>
    {
    }
}