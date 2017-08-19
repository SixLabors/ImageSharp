// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Benchmarks.Color.Bulk
{
    using BenchmarkDotNet.Attributes;

    using SixLabors.ImageSharp.Memory;
    using SixLabors.ImageSharp.PixelFormats;

    public abstract class ToXyz<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private Buffer<TPixel> source;

        private Buffer<byte> destination;

        [Params(16, 128, 1024)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.source = new Buffer<TPixel>(this.Count);
            this.destination = new Buffer<byte>(this.Count * 3);
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
            TPixel[] s = this.source.Array;
            byte[] d = this.destination.Array;

            for (int i = 0; i < this.Count; i++)
            {
                TPixel c = s[i];
                c.ToXyzBytes(d, i * 4);
            }
        }

        [Benchmark]
        public void CommonBulk()
        {
            new PixelOperations<TPixel>().ToRgb24Bytes(this.source, this.destination, this.Count);
        }

        [Benchmark]
        public void OptimizedBulk()
        {
            PixelOperations<TPixel>.Instance.ToRgb24Bytes(this.source, this.destination, this.Count);
        }
    }

    public class ToXyz_Rgba32 : ToXyz<Rgba32>
    {
    }
}