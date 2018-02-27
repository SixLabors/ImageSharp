// ReSharper disable InconsistentNaming
namespace SixLabors.ImageSharp.Benchmarks.Color.Bulk
{
    using System.Numerics;
    using System.Runtime.CompilerServices;

    using BenchmarkDotNet.Attributes;

    using SixLabors.ImageSharp.Memory;
    using SixLabors.ImageSharp.PixelFormats;

    [Config(typeof(Config.ShortClr))]
    public abstract class PackFromVector4<TPixel>
        where TPixel : struct, IPixel<TPixel>
    {
        private IBuffer<Vector4> source;

        private IBuffer<TPixel> destination;

        [Params(16, 128, 512)]
        public int Count { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            this.destination = Configuration.Default.MemoryManager.Allocate<TPixel>(this.Count);
            this.source = Configuration.Default.MemoryManager.Allocate<Vector4>(this.Count);
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
            ref Vector4 s = ref this.source.Span.DangerousGetPinnableReference();
            ref TPixel d = ref this.destination.Span.DangerousGetPinnableReference();
            
            for (int i = 0; i < this.Count; i++)
            {
                Unsafe.Add(ref d, i).PackFromVector4(Unsafe.Add(ref s, i));
            }
        }

        [Benchmark]
        public void CommonBulk()
        {
            new PixelOperations<TPixel>().PackFromVector4(this.source.Span, this.destination.Span, this.Count);
        }

        [Benchmark]
        public void OptimizedBulk()
        {
            PixelOperations<TPixel>.Instance.PackFromVector4(this.source.Span, this.destination.Span, this.Count);
        }
    }

    public class PackFromVector4_Rgba32 : PackFromVector4<Rgba32>
    {

    }
}