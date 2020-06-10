// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;

using Colourful;
using Colourful.Conversion;

using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;

namespace SixLabors.ImageSharp.Benchmarks.ColorSpaces
{
    public class RgbWorkingSpaceAdapt
    {
        private static readonly Rgb Rgb = new Rgb(0.206162F, 0.260277F, 0.746717F, RgbWorkingSpaces.WideGamutRgb);

        private static readonly RGBColor RGBColor = new RGBColor(0.206162, 0.260277, 0.746717, RGBWorkingSpaces.WideGamutRGB);

        private static readonly ColorSpaceConverter ColorSpaceConverter = new ColorSpaceConverter(new ColorSpaceConverterOptions { TargetRgbWorkingSpace = RgbWorkingSpaces.SRgb });

        private static readonly ColourfulConverter ColourfulConverter = new ColourfulConverter { TargetRGBWorkingSpace = RGBWorkingSpaces.sRGB };

        [Benchmark(Baseline = true, Description = "Colourful Adapt")]
        public RGBColor ColourfulConvert()
        {
            return ColourfulConverter.Adapt(RGBColor);
        }

        [Benchmark(Description = "ImageSharp Adapt")]
        internal Rgb ColorSpaceConvert()
        {
            return ColorSpaceConverter.Adapt(Rgb);
        }
    }
}
