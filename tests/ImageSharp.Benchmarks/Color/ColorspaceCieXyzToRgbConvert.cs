// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using BenchmarkDotNet.Attributes;

using Colourful;

using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.ColorSpaces.Conversion;

namespace SixLabors.ImageSharp.Benchmarks.ColorSpaces
{
    public class ColorspaceCieXyzToRgbConvert
    {
        private static readonly CieXyz CieXyz = new CieXyz(0.95047F, 1, 1.08883F);

        private static readonly XYZColor XYZColor = new XYZColor(0.95047, 1, 1.08883);

        private static readonly ColorSpaceConverter ColorSpaceConverter = new ColorSpaceConverter();

        private static readonly IColorConverter<XYZColor, RGBColor> ColourfulConverter = new ConverterBuilder().FromXYZ(RGBWorkingSpaces.sRGB.WhitePoint).ToRGB(RGBWorkingSpaces.sRGB).Build();

        [Benchmark(Baseline = true, Description = "Colourful Convert")]
        public double ColourfulConvert()
        {
            return ColourfulConverter.Convert(XYZColor).R;
        }

        [Benchmark(Description = "ImageSharp Convert")]
        public float ColorSpaceConvert()
        {
            return ColorSpaceConverter.ToRgb(CieXyz).R;
        }
    }
}
