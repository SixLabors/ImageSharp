// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg
{
    [Config(typeof(Config.ShortMultiFramework))]
    public class RgbColorConversion : ColorConversionBenchmark
    {
        public RgbColorConversion()
            : base(3)
        {
        }

        [Benchmark(Baseline = true)]
        public void Scalar()
        {
            var values = new JpegColorConverter.ComponentValues(this.Input, 0);

            new JpegColorConverter.FromRgbScalar(8).ConvertToRgbInplace(values);
        }

        [Benchmark]
        public void SimdVector8()
        {
            var values = new JpegColorConverter.ComponentValues(this.Input, 0);

            new JpegColorConverter.FromRgbVector8(8).ConvertToRgbInplace(values);
        }

#if SUPPORTS_RUNTIME_INTRINSICS
        [Benchmark]
        public void SimdVectorAvx()
        {
            var values = new JpegColorConverter.ComponentValues(this.Input, 0);

            new JpegColorConverter.FromRgbAvx(8).ConvertToRgbInplace(values);
        }
#endif
    }
}
