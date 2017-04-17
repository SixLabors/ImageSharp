// ReSharper disable InconsistentNaming
namespace ImageSharp.Benchmarks.Color.Bulk
{
    using BenchmarkDotNet.Attributes;

    using Color = ImageSharp.Color;

    public abstract class PackFromXyzw<TColor>
        where TColor : struct, IPixel<TColor>
    {
        private Buffer<TColor> destination;

        private Buffer<byte> source;

        [Params(16, 128, 1024)]
        public int Count { get; set; }

        [Setup]
        public void Setup()
        {
            this.destination = new Buffer<TColor>(this.Count);
            this.source = new Buffer<byte>(this.Count * 4);
        }

        [Cleanup]
        public void Cleanup()
        {
            this.destination.Dispose();
            this.source.Dispose();
        }

        [Benchmark(Baseline = true)]
        public void PerElement()
        {
            byte[] s = this.source.Array;
            TColor[] d = this.destination.Array;
            
            for (int i = 0; i < this.Count; i++)
            {
                int i4 = i * 4;
                TColor c = default(TColor);
                c.PackFromBytes(s[i4], s[i4 + 1], s[i4 + 2], s[i4 + 3]);
                d[i] = c;
            }
        }

        [Benchmark]
        public void CommonBulk()
        {
            new BulkPixelOperations<TColor>().PackFromXyzwBytes(this.source, this.destination, this.Count);
        }

        [Benchmark]
        public void OptimizedBulk()
        {
            BulkPixelOperations<TColor>.Instance.PackFromXyzwBytes(this.source, this.destination, this.Count);
        }
    }

    public class PackFromXyzw_Color : PackFromXyzw<Color>
    {
    }
}