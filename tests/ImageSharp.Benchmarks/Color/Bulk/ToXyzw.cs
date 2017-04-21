using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
// ReSharper disable InconsistentNaming

namespace ImageSharp.Benchmarks.Color.Bulk
{
    using BenchmarkDotNet.Attributes;
    using ImageSharp.PixelFormats;

    public abstract class ToXyzw<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private Buffer<TPixel> source;

        private Buffer<byte> destination;

        [Params(16, 128, 1024)]
        public int Count { get; set; }

        [Setup]
        public void Setup()
        {
            this.source = new Buffer<TPixel>(this.Count);
            this.destination = new Buffer<byte>(this.Count * 4);
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
            byte[] d = this.destination.Array;

            for (int i = 0; i < this.Count; i++)
            {
                TPixel c = s[i];
                c.ToXyzwBytes(d, i * 4);
            }
        }

        [Benchmark]
        public void CommonBulk()
        {
            new BulkPixelOperations<TPixel>().ToXyzwBytes(this.source, this.destination, this.Count);
        }

        [Benchmark]
        public void OptimizedBulk()
        {
            BulkPixelOperations<TPixel>.Instance.ToXyzwBytes(this.source, this.destination, this.Count);
        }
    }
    
    public class ToXyzw_Color : ToXyzw<Rgba32>
    {
    }

    public class ToXyzw_Argb : ToXyzw<Argb32>
    {
    }
}
