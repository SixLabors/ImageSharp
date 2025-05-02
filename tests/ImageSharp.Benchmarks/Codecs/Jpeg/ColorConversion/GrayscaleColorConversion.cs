// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg.Components;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg;

[Config(typeof(Config.Short))]
public class GrayscaleColorConversion : ColorConversionBenchmark
{
    public GrayscaleColorConversion()
        : base(1)
    {
    }

    [Benchmark(Baseline = true)]
    public void Scalar()
    {
        var values = new JpegColorConverterBase.ComponentValues(this.Input, 0);

        new JpegColorConverterBase.GrayscaleScalar(8).ConvertToRgbInPlace(values);
    }

    [Benchmark]
    public void SimdVector8()
    {
        var values = new JpegColorConverterBase.ComponentValues(this.Input, 0);

        new JpegColorConverterBase.GrayScaleVector(8).ConvertToRgbInPlace(values);
    }
}
