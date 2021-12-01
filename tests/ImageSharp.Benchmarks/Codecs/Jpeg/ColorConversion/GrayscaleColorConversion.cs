// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg
{
    [Config(typeof(Config.ShortMultiFramework))]
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

            new JpegColorConverterBase.FromGrayscaleBasic(8).ConvertToRgbInplace(values);
        }

        [Benchmark]
        public void SimdVectorAvx2()
        {
            var values = new JpegColorConverterBase.ComponentValues(this.Input, 0);

            new JpegColorConverterBase.FromGrayscaleAvx2(8).ConvertToRgbInplace(values);
        }
    }
}
