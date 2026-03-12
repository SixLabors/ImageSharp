// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using Colourful;
using SixLabors.ImageSharp.ColorProfiles;

namespace SixLabors.ImageSharp.Benchmarks.ColorProfiles;

public class RgbWorkingSpaceAdapt
{
    private static readonly Rgb Rgb = new(0.206162F, 0.260277F, 0.746717F);

    private static readonly RGBColor RGBColor = new(0.206162, 0.260277, 0.746717);

    private static readonly ColorProfileConverter ColorProfileConverter = new(new ColorConversionOptions { SourceRgbWorkingSpace = KnownRgbWorkingSpaces.WideGamutRgb, TargetRgbWorkingSpace = KnownRgbWorkingSpaces.SRgb });

    private static readonly IColorConverter<RGBColor, RGBColor> ColourfulConverter = new ConverterBuilder().FromRGB(RGBWorkingSpaces.WideGamutRGB).ToRGB(RGBWorkingSpaces.sRGB).Build();

    [Benchmark(Baseline = true, Description = "Colourful Adapt")]
    public RGBColor ColourfulConvert() => ColourfulConverter.Convert(RGBColor);

    [Benchmark(Description = "ImageSharp Adapt")]
    public Rgb ColorSpaceConvert() => ColorProfileConverter.Convert<Rgb, Rgb>(Rgb);
}
