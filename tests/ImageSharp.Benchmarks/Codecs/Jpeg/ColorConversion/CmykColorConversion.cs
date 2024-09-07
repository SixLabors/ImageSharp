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

        new JpegColorConverterBase.CmykScalar(8).ConvertToRgbInplace(values);
    }

    [Benchmark]
    public void SimdVector8()
    {
        JpegColorConverterBase.ComponentValues values = new(this.Input, 0);

        new JpegColorConverterBase.CmykVector(8).ConvertToRgbInplace(values);
    }

    [Benchmark]
    public void SimdVectorAvx()
    {
        JpegColorConverterBase.ComponentValues values = new(this.Input, 0);

        new JpegColorConverterBase.CmykAvx(8).ConvertToRgbInplace(values);
    }

    [Benchmark]
    public void SimdVectorArm64()
    {
        JpegColorConverterBase.ComponentValues values = new(this.Input, 0);

        new JpegColorConverterBase.CmykArm64(8).ConvertToRgbInplace(values);
    }
}
