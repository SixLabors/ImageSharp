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
        JpegColorConverterBase.ComponentValues values = new(this.Input, 0);

        new JpegColorConverterBase.YCbCrScalar(8).ConvertToRgbInPlace(values);
    }

    [Benchmark]
    public void SimdVector8()
    {
        JpegColorConverterBase.ComponentValues values = new(this.Input, 0);

        new JpegColorConverterBase.YCbCrVector(8).ConvertToRgbInPlace(values);
    }

    [Benchmark]
    public void SimdVector128()
    {
        JpegColorConverterBase.ComponentValues values = new(this.Input, 0);

        new JpegColorConverterBase.YCbCrVector128(8).ConvertToRgbInPlace(values);
    }

    [Benchmark]
    public void SimdVector256()
    {
        JpegColorConverterBase.ComponentValues values = new(this.Input, 0);

        new JpegColorConverterBase.YCbCrVector256(8).ConvertToRgbInPlace(values);
    }

    [Benchmark]
    public void SimdVector512()
    {
        JpegColorConverterBase.ComponentValues values = new(this.Input, 0);

        new JpegColorConverterBase.YCbCrVector512(8).ConvertToRgbInPlace(values);
    }
}
