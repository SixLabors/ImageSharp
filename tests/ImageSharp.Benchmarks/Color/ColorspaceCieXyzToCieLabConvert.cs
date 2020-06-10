// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;

using Colourful;
using Colourful.Conversion;

using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;

namespace SixLabors.ImageSharp.Benchmarks.ColorSpaces
{
    public class ColorspaceCieXyzToCieLabConvert
    {
        private static readonly CieXyz CieXyz = new CieXyz(0.95047F, 1, 1.08883F);

        private static readonly XYZColor XYZColor = new XYZColor(0.95047, 1, 1.08883);

        private static readonly ColorSpaceConverter ColorSpaceConverter = new ColorSpaceConverter();

        private static readonly ColourfulConverter ColourfulConverter = new ColourfulConverter();

        [Benchmark(Baseline = true, Description = "Colourful Convert")]
        public double ColourfulConvert()
        {
            return ColourfulConverter.ToLab(XYZColor).L;
        }

        [Benchmark(Description = "ImageSharp Convert")]
        public float ColorSpaceConvert()
        {
            return ColorSpaceConverter.ToCieLab(CieXyz).L;
        }
    }
}
