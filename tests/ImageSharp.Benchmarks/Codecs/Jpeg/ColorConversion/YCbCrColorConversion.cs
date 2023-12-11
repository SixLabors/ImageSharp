// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg.Components;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg;

[Config(typeof(Config.Short))]
public class YCbCrColorConversion : ColorConversionBenchmark
{
    public YCbCrColorConversion()
        : base(3)
    {
    }

    [Benchmark]
    public void Scalar()
    {
        var values = new JpegColorConverterBase.ComponentValues(this.Input, 0);

        new JpegColorConverterBase.YCbCrScalar(8).ConvertToRgbInplace(values);
    }

    [Benchmark]
    public void SimdVector8()
    {
        var values = new JpegColorConverterBase.ComponentValues(this.Input, 0);

        new JpegColorConverterBase.YCbCrVector(8).ConvertToRgbInplace(values);
    }

    [Benchmark]
    public void SimdVectorAvx()
    {
        var values = new JpegColorConverterBase.ComponentValues(this.Input, 0);

        new JpegColorConverterBase.YCbCrAvx(8).ConvertToRgbInplace(values);
    }

    [Benchmark]
    public void SimdVectorArm()
    {
        var values = new JpegColorConverterBase.ComponentValues(this.Input, 0);

        new JpegColorConverterBase.YCbCrArm(8).ConvertToRgbInplace(values);
    }
}
