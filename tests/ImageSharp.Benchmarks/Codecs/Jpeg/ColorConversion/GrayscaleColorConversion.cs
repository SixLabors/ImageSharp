// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg.Components;

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

            new JpegColorConverterBase.GrayscaleScalar(8).ConvertToRgbInplace(values);
        }

#if SUPPORTS_RUNTIME_INTRINSICS
        [Benchmark]
        public void SimdVectorAvx()
        {
            var values = new JpegColorConverterBase.ComponentValues(this.Input, 0);

            new JpegColorConverterBase.GrayscaleAvx(8).ConvertToRgbInplace(values);
        }
#endif
    }
}
