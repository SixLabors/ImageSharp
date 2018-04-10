// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Benchmarks.ColorSpaces.Bulk
{
    using System;

    using BenchmarkDotNet.Attributes;

    using SixLabors.ImageSharp.Memory;
    using SixLabors.ImageSharp.PixelFormats;

    public abstract class PackFromXyzw<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private IBuffer<TPixel> destination;

        private IBuffer<byte> source;

        [Params(16, 128, 1024)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.destination = Configuration.Default.MemoryManager.Allocate<TPixel>(this.Count);
            this.source = Configuration.Default.MemoryManager.Allocate<byte>(this.Count * 4);
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            this.destination.Dispose();
            this.source.Dispose();
        }

        [Benchmark(Baseline = true)]
        public void PerElement()
        {
            Span<byte> s = this.source.Span;
            Span<TPixel> d = this.destination.Span;
            
            for (int i = 0; i < this.Count; i++)
            {
                int i4 = i * 4;
                var c = default(TPixel);
                c.PackFromRgba32(new Rgba32(s[i4], s[i4 + 1], s[i4 + 2], s[i4 + 3]));
                d[i] = c;
            }
        }

        [Benchmark]
        public void CommonBulk()
        {
            new PixelOperations<TPixel>().PackFromRgba32Bytes(this.source.Span, this.destination.Span, this.Count);
        }

        [Benchmark]
        public void OptimizedBulk()
        {
           PixelOperations<TPixel>.Instance.PackFromRgba32Bytes(this.source.Span, this.destination.Span, this.Count);
        }
    }

    public class PackFromXyzw_Rgba32 : PackFromXyzw<Rgba32>
    {
    }
}