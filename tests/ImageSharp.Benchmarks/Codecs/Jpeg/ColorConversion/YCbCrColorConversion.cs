// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg
{
    [Config(typeof(Config.ShortMultiFramework))]
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

            new JpegColorConverterBase.FromYCbCrBasic(8).ConvertToRgbInplace(values);
        }

        [Benchmark(Baseline = true)]
        public void SimdVector()
        {
            var values = new JpegColorConverterBase.ComponentValues(this.Input, 0);

            new JpegColorConverterBase.FromYCbCrVector4(8).ConvertToRgbInplace(values);
        }

        [Benchmark]
        public void SimdVector8()
        {
            var values = new JpegColorConverterBase.ComponentValues(this.Input, 0);

            new JpegColorConverterBase.FromYCbCrVector8(8).ConvertToRgbInplace(values);
        }

        [Benchmark]
        public void SimdVectorAvx2()
        {
            var values = new JpegColorConverterBase.ComponentValues(this.Input, 0);

            new JpegColorConverterBase.FromYCbCrAvx2(8).ConvertToRgbInplace(values);
        }
    }
}
