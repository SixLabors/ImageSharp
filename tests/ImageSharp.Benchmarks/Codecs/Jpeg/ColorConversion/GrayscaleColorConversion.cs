// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg.Components;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg;

[Config(typeof(Config.Short))]
public class GrayScaleColorConversion : ColorConversionBenchmark
{
    private readonly JpegColorConverterBase converter = JpegColorConverterBase.GetConverter(JpegColorSpace.Grayscale, 8);

    /// <summary>
    /// Initializes a new instance of the <see cref="GrayScaleColorConversion"/> class.
    /// </summary>
    public GrayScaleColorConversion()
        : base(1)
    {
    }

    /// <summary>
    /// Converts one grayscale component row through the adaptive operator traversal.
    /// </summary>
    [Benchmark]
    public void ConvertToRgb()
    {
        JpegColorConverterBase.ComponentValues values = new(this.Input, 0);

        this.converter.ConvertToRgbInPlace(values);
    }
}
