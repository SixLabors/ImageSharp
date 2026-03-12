// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using Colourful;
using SixLabors.ImageSharp.ColorProfiles;
using Illuminants = Colourful.Illuminants;

namespace SixLabors.ImageSharp.Benchmarks.ColorProfiles;

public class ColorspaceCieXyzToCieLabConvert
{
    private static readonly CieXyz CieXyz = new(0.95047F, 1, 1.08883F);

    private static readonly XYZColor XYZColor = new(0.95047, 1, 1.08883);

    private static readonly ColorProfileConverter ColorProfileConverter = new();

    private static readonly IColorConverter<XYZColor, LabColor> ColourfulConverter = new ConverterBuilder().FromXYZ(Illuminants.D50).ToLab(Illuminants.D50).Build();

    [Benchmark(Baseline = true, Description = "Colourful Convert")]
    public double ColourfulConvert() => ColourfulConverter.Convert(XYZColor).L;

    [Benchmark(Description = "ImageSharp Convert")]
    public float ColorSpaceConvert() => ColorProfileConverter.Convert<CieXyz, CieLab>(CieXyz).L;
}
