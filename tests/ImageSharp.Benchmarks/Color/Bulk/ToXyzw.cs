using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
// ReSharper disable InconsistentNaming

namespace ImageSharp.Benchmarks.Color.Bulk
{
    using BenchmarkDotNet.Attributes;

    using Color = ImageSharp.Color;

    public abstract class ToXyzw<TColor>
        where TColor : struct, IPixel<TColor>
    {
        private Buffer<TColor> source;

        private Buffer<byte> destination;

        [Params(16, 128, 1024)]
        public int Count { get; set; }

        [Setup]
        public void Setup()
        {
            this.source = new Buffer<TColor>(this.Count);
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
            TColor[] s = this.source.Array;
            byte[] d = this.destination.Array;

            for (int i = 0; i < this.Count; i++)
            {
                TColor c = s[i];
                c.ToXyzwBytes(d, i * 4);
            }
        }

        [Benchmark]
        public void CommonBulk()
        {
            new BulkPixelOperations<TColor>().ToXyzwBytes(this.source, this.destination, this.Count);
        }

        [Benchmark]
        public void OptimizedBulk()
        {
            BulkPixelOperations<TColor>.Instance.ToXyzwBytes(this.source, this.destination, this.Count);
        }
    }
    
    public class ToXyzw_Color : ToXyzw<Color>
    {
    }

    public class ToXyzw_Argb : ToXyzw<Argb>
    {
    }
}
