// ReSharper disable InconsistentNaming
namespace ImageSharp.Benchmarks.Color.Bulk
{
    using BenchmarkDotNet.Attributes;

    using Color32 = ImageSharp.Color32;

    public abstract class ToXyz<TColor>
        where TColor : struct, IPixel<TColor>
    {
        private PinnedBuffer<TColor> source;

        private PinnedBuffer<byte> destination;

        [Params(16, 128, 1024)]
        public int Count { get; set; }

        [Setup]
        public void Setup()
        {
            this.source = new PinnedBuffer<TColor>(this.Count);
            this.destination = new PinnedBuffer<byte>(this.Count * 3);
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
            byte[] d = this.destination.Array;

            for (int i = 0; i < this.Count; i++)
            {
                TColor c = s[i];
                c.ToXyzBytes(d, i * 4);
            }
        }

        [Benchmark]
        public void CommonBulk()
        {
            new BulkPixelOperations<TColor>().ToXyzBytes(this.source, this.destination, this.Count);
        }

        [Benchmark]
        public void OptimizedBulk()
        {
            BulkPixelOperations<TColor>.Instance.ToXyzBytes(this.source, this.destination, this.Count);
        }
    }

    public class ToXyz_Color : ToXyz<Color32>
    {
    }
}