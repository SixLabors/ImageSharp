// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;

using Colourful;

using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;
using Illuminants = Colourful.Illuminants;

namespace SixLabors.ImageSharp.Benchmarks.ColorSpaces
{
    public class ColorspaceCieXyzToHunterLabConvert
    {
        private static readonly CieXyz CieXyz = new CieXyz(0.95047F, 1, 1.08883F);

        private static readonly XYZColor XYZColor = new XYZColor(0.95047, 1, 1.08883);

        private static readonly ColorSpaceConverter ColorSpaceConverter = new ColorSpaceConverter();

        private static readonly IColorConverter<XYZColor, HunterLabColor> ColourfulConverter = new ConverterBuilder().FromXYZ(Illuminants.C).ToHunterLab(Illuminants.C).Build();

        [Benchmark(Baseline = true, Description = "Colourful Convert")]
        public double ColourfulConvert()
        {
            return ColourfulConverter.Convert(XYZColor).L;
        }

        [Benchmark(Description = "ImageSharp Convert")]
        public float ColorSpaceConvert()
        {
            return ColorSpaceConverter.ToHunterLab(CieXyz).L;
        }
    }
}
