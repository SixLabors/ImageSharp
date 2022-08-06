// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg.Components;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg
{
    [Config(typeof(Config.ShortMultiFramework))]
    public class YccKColorConverter : ColorConversionBenchmark
    {
        public YccKColorConverter()
            : base(4)
        {
        }

        [Benchmark(Baseline = true)]
        public void Scalar()
        {
            var values = new JpegColorConverterBase.ComponentValues(this.Input, 0);

            new JpegColorConverterBase.YccKScalar(8).ConvertToRgbInplace(values);
        }

        [Benchmark]
        public void SimdVector8()
        {
            var values = new JpegColorConverterBase.ComponentValues(this.Input, 0);

            new JpegColorConverterBase.YccKVector(8).ConvertToRgbInplace(values);
        }

#if SUPPORTS_RUNTIME_INTRINSICS
        [Benchmark]
        public void SimdVectorAvx2()
        {
            var values = new JpegColorConverterBase.ComponentValues(this.Input, 0);

            new JpegColorConverterBase.YccKAvx(8).ConvertToRgbInplace(values);
        }
#endif
    }
}
