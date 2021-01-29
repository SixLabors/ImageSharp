// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;
using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.Formats.Jpeg.Components;
using SixLabors.ImageSharp.Formats.Jpeg.Components.Encoder;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Benchmarks.Format.Jpeg.Components.Encoder
{
    public class YCbCrForwardConverterBenchmark
    {
        private RgbToYCbCrConverterLut converter;
        private Rgb24[] data;

        [GlobalSetup]
        public void Setup()
        {
            this.converter = RgbToYCbCrConverterLut.Create();

            var r = new Random(42);
            this.data = new Rgb24[64];

            var d = new byte[3];
            for (int i = 0; i < this.data.Length; i++)
            {
                r.NextBytes(d);
                this.data[i] = new Rgb24(d[0], d[1], d[2]);
            }
        }

        [Benchmark(Baseline = true)]
        public void ConvertLut()
        {
            Block8x8F y = default;
            Block8x8F cb = default;
            Block8x8F cr = default;

            this.converter.Convert(this.data.AsSpan(), ref y, ref cb, ref cr);
        }

        [Benchmark]
        public void ConvertVectorized()
        {
            Block8x8F y = default;
            Block8x8F cb = default;
            Block8x8F cr = default;

            if (RgbToYCbCrConverterVectorized.IsSupported)
            {
                RgbToYCbCrConverterVectorized.Convert(this.data.AsSpan(), ref y, ref cb, ref cr);
            }
        }
    }
}
