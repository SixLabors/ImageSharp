// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg.Components;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg;

[Config(typeof(Config.Short))]
public class CmykColorConversion : ColorConversionBenchmark
{
    public CmykColorConversion()
        : base(4)
    {
    }

    [Benchmark(Baseline = true)]
    public void Scalar()
    {
        var values = new JpegColorConverterBase.ComponentValues(this.Input, 0);

        new JpegColorConverterBase.CmykScalar(8).ConvertToRgbInPlace(values);
    }

    [Benchmark]
    public void SimdVector8()
    {
        var values = new JpegColorConverterBase.ComponentValues(this.Input, 0);

        new JpegColorConverterBase.CmykVector(8).ConvertToRgbInPlace(values);
    }
}
