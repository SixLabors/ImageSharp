// ReSharper disable InconsistentNaming
namespace ImageSharp.Benchmarks.Color.Bulk
{
    using System.Numerics;

    using BenchmarkDotNet.Attributes;

    public abstract class ToVector4<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private Buffer<TPixel> source;

        private Buffer<Vector4> destination;

        [Params(64, 300, 1024)]
        public int Count { get; set; }

        [Setup]
        public void Setup()
        {
            this.source = new Buffer<TPixel>(this.Count);
            this.destination = new Buffer<Vector4>(this.Count);
        }

        [Cleanup]
        public void Cleanup()
        {
            this.source.Dispose();
            this.destination.Dispose();
        }

        [Benchmark(Baseline = true)]
        public void PerElement()
        {
            TPixel[] s = this.source.Array;
            Vector4[] d = this.destination.Array;

            for (int i = 0; i < this.Count; i++)
            {
                TPixel c = s[i];
                d[i] = c.ToVector4();
            }
        }

        [Benchmark]
        public void CommonBulk()
        {
            new BulkPixelOperations<TPixel>().ToVector4(this.source, this.destination, this.Count);
        }

        [Benchmark]
        public void OptimizedBulk()
        {
            BulkPixelOperations<TPixel>.Instance.ToVector4(this.source, this.destination, this.Count);
        }
    }

    public class ToVector4_Color : ToVector4<ImageSharp.Rgba32>
    {
    }
}