// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Benchmarks.Color.Bulk
{
    using BenchmarkDotNet.Attributes;

    using SixLabors.ImageSharp.Memory;
    using SixLabors.ImageSharp.PixelFormats;

    public abstract class PackFromXyzw<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private Buffer<TPixel> destination;

        private Buffer<byte> source;

        [Params(16, 128, 1024)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.destination = new Buffer<TPixel>(this.Count);
            this.source = new Buffer<byte>(this.Count * 4);
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
            byte[] s = this.source.Array;
            TPixel[] d = this.destination.Array;
            
            for (int i = 0; i < this.Count; i++)
            {
                int i4 = i * 4;
                TPixel c = default(TPixel);
                c.PackFromRgba32(new Rgba32(s[i4], s[i4 + 1], s[i4 + 2], s[i4 + 3]));
                d[i] = c;
            }
        }

        [Benchmark]
        public void CommonBulk()
        {
            new PixelOperations<TPixel>().PackFromRgba32Bytes(this.source, this.destination, this.Count);
        }

        [Benchmark]
        public void OptimizedBulk()
        {
           PixelOperations<TPixel>.Instance.PackFromRgba32Bytes(this.source, this.destination, this.Count);
        }
    }

    public class PackFromXyzw_Rgba32 : PackFromXyzw<Rgba32>
    {
    }
}