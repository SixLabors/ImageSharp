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
        JpegColorConverterBase.ComponentValues values = new(this.Input, 0);

        new JpegColorConverterBase.CmykScalar(8).ConvertToRgbInPlace(values);
    }

    [Benchmark]
    public void SimdVector128()
    {
        JpegColorConverterBase.ComponentValues values = new(this.Input, 0);

        new JpegColorConverterBase.CmykVector128(8).ConvertToRgbInPlace(values);
    }

    [Benchmark]
    public void SimdVector256()
    {
        JpegColorConverterBase.ComponentValues values = new(this.Input, 0);

        new JpegColorConverterBase.CmykVector256(8).ConvertToRgbInPlace(values);
    }

    [Benchmark]
    public void SimdVector512()
    {
        JpegColorConverterBase.ComponentValues values = new(this.Input, 0);

        new JpegColorConverterBase.CmykVector512(8).ConvertToRgbInPlace(values);
    }
}
