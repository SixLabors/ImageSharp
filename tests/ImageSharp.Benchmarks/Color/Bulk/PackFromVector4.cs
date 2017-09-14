// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Benchmarks.Color.Bulk
{
    using System.Numerics;

    using BenchmarkDotNet.Attributes;

    using SixLabors.ImageSharp.Memory;
    using SixLabors.ImageSharp.PixelFormats;

    [Config(typeof(Config.Short))]
    public abstract class PackFromVector4<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private Buffer<Vector4> source;

        private Buffer<TPixel> destination;

        [Params(16, 128, 512)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.destination = new Buffer<TPixel>(this.Count);
            this.source = new Buffer<Vector4>(this.Count);
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
            Vector4[] s = this.source.Array;
            TPixel[] d = this.destination.Array;
            
            for (int i = 0; i < this.Count; i++)
            {
                d[i].PackFromVector4(s[i]);
            }
        }

        [Benchmark]
        public void CommonBulk()
        {
            new PixelOperations<TPixel>().PackFromVector4(this.source, this.destination, this.Count);
        }

        [Benchmark]
        public void OptimizedBulk()
        {
            PixelOperations<TPixel>.Instance.PackFromVector4(this.source, this.destination, this.Count);
        }
    }

    public class PackFromVector4_Rgba32 : PackFromVector4<Rgba32>
    {

    }
}