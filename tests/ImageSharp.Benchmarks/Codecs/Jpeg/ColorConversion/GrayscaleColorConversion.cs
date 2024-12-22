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
        JpegColorConverterBase.ComponentValues values = new JpegColorConverterBase.ComponentValues(this.Input, 0);

        new JpegColorConverterBase.GrayscaleScalar(8).ConvertToRgbInplace(values);
    }

    [Benchmark]
    public void SimdVectorAvx()
    {
        JpegColorConverterBase.ComponentValues values = new JpegColorConverterBase.ComponentValues(this.Input, 0);

        new JpegColorConverterBase.GrayscaleAvx(8).ConvertToRgbInplace(values);
    }

    [Benchmark]
    public void SimdVectorArm()
    {
        JpegColorConverterBase.ComponentValues values = new JpegColorConverterBase.ComponentValues(this.Input, 0);

        new JpegColorConverterBase.GrayscaleArm(8).ConvertToRgbInplace(values);
    }
}
