// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg.Components;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg;

[Config(typeof(Config.Short))]
public class RgbColorConversion : ColorConversionBenchmark
{
    public RgbColorConversion()
        : base(3)
    {
    }

    [Benchmark(Baseline = true)]
    public void Scalar()
    {
        JpegColorConverterBase.ComponentValues values = new(this.Input, 0);

        new JpegColorConverterBase.RgbScalar(8).ConvertToRgbInplace(values);
    }

    [Benchmark]
    public void SimdVector8()
    {
        JpegColorConverterBase.ComponentValues values = new(this.Input, 0);

        new JpegColorConverterBase.RgbVector(8).ConvertToRgbInplace(values);
    }

    [Benchmark]
    public void SimdVectorAvx()
    {
        JpegColorConverterBase.ComponentValues values = new(this.Input, 0);

        new JpegColorConverterBase.RgbAvx(8).ConvertToRgbInplace(values);
    }

    [Benchmark]
    public void SimdVectorArm()
    {
        JpegColorConverterBase.ComponentValues values = new(this.Input, 0);

        new JpegColorConverterBase.RgbArm(8).ConvertToRgbInplace(values);
    }
}
