// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Decoder.ColorConverters;

namespace SixLabors.ImageSharp.Benchmarks.Codecs.Jpeg
{
    [Config(typeof(Config.ShortMultiFramework))]
    public class CmykColorConversion : ColorConversionBenchmark
    {
        public CmykColorConversion()
            : base(4)
        {
        }

        [Benchmark(Baseline = true)]
        public void Scalar()
        {
            var values = new JpegColorConverter.ComponentValues(this.Input, 0);

            new JpegColorConverter.FromCmykBasic(8).ConvertToRgba(values, this.Output);
        }

        [Benchmark]
        public void SimdVector8()
        {
            var values = new JpegColorConverter.ComponentValues(this.Input, 0);

            new JpegColorConverter.FromCmykVector8(8).ConvertToRgba(values, this.Output);
        }

        [Benchmark]
        public void SimdVectorAvx2()
        {
            var values = new JpegColorConverter.ComponentValues(this.Input, 0);

            new JpegColorConverter.FromCmykAvx2(8).ConvertToRgba(values, this.Output);
        }
    }
}
