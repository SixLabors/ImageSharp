// ReSharper disable InconsistentNaming
namespace ImageSharp.Benchmarks.Color.Bulk
{
    using System.Numerics;

    using BenchmarkDotNet.Attributes;

    public abstract class ToVector4<TColor>
        where TColor : struct, IPixel<TColor>
    {
        private PinnedBuffer<TColor> source;

        private PinnedBuffer<Vector4> destination;

        [Params(16, 128, 1024)]
        public int Count { get; set; }

        [Setup]
        public void Setup()
        {
            this.source = new PinnedBuffer<TColor>(this.Count);
            this.destination = new PinnedBuffer<Vector4>(this.Count);
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
            TColor[] s = this.source.Array;
            Vector4[] d = this.destination.Array;

            for (int i = 0; i < this.Count; i++)
            {
                TColor c = s[i];
                d[i] = c.ToVector4();
            }
        }

        [Benchmark]
        public void CommonBulk()
        {
            new BulkPixelOperations<TColor>().ToVector4(this.source, this.destination, this.Count);
        }

        [Benchmark]
        public void OptimizedBulk()
        {
            BulkPixelOperations<TColor>.Instance.ToVector4(this.source, this.destination, this.Count);
        }
    }

    public class ToVector4_Color : ToVector4<ImageSharp.Color>
    {
    }
}