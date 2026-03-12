// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using Colourful;
using SixLabors.ImageSharp.ColorProfiles;
using Illuminants = Colourful.Illuminants;

namespace SixLabors.ImageSharp.Benchmarks.ColorProfiles;

public class ColorspaceCieXyzToHunterLabConvert
{
    private static readonly CieXyz CieXyz = new(0.95047F, 1, 1.08883F);

    private static readonly XYZColor XYZColor = new(0.95047, 1, 1.08883);

    private static readonly ColorProfileConverter ColorProfileConverter = new();

    private static readonly IColorConverter<XYZColor, HunterLabColor> ColourfulConverter = new ConverterBuilder().FromXYZ(Illuminants.C).ToHunterLab(Illuminants.C).Build();

    [Benchmark(Baseline = true, Description = "Colourful Convert")]
    public double ColourfulConvert() => ColourfulConverter.Convert(XYZColor).L;

    [Benchmark(Description = "ImageSharp Convert")]
    public float ColorSpaceConvert() => ColorProfileConverter.Convert<CieXyz, HunterLab>(CieXyz).L;
}
