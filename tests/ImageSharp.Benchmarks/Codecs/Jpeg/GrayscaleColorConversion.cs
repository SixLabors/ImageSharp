using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg
{
    [Config(typeof(Config.ShortClr))]
    public class GrayscaleColorConversion : ColorConversionBenchmark
    {
        public GrayscaleColorConversion()
            : base(1)
        {
        }

        [Benchmark(Baseline = true)]
        public void Scalar()
        {
            var values = new JpegColorConverter.ComponentValues(this.input, 0);

            new JpegColorConverter.FromGrayscaleBasic(8).ConvertToRgba(values, this.output);
        }

        [Benchmark]
        public void SimdVectorAvx2()
        {
            var values = new JpegColorConverter.ComponentValues(this.input, 0);

            new JpegColorConverter.FromGrayscaleAvx2(8).ConvertToRgba(values, this.output);
        }
    }
}
