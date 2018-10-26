using BenchmarkDotNet.Attributes;

using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.ColorSpaces.Bulk
{
    [Config(typeof(Config.ShortClr))]
    public class ToVector4_Bgra32 : ToVector4<Bgra32>
    {
        [Benchmark(Baseline = true)]
        public void PixelOperations_Base()
        {
            new PixelOperations<Bgra32>().ToVector4(
                this.Configuration,
                this.source.GetSpan(),
                this.destination.GetSpan());
        }
    }
}