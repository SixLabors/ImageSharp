// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using BenchmarkDotNet.Attributes;
using SixLabors.ImageSharp.PixelFormats;
using SystemColor = System.Drawing.Color;

namespace SixLabors.ImageSharp.Benchmarks;

public class ColorEquality
{
    [Benchmark(Baseline = true, Description = "System.Drawing Color Equals")]
    public bool SystemDrawingColorEqual()
        => SystemColor.FromArgb(128, 128, 128, 128).Equals(SystemColor.FromArgb(128, 128, 128, 128));

    [Benchmark(Description = "ImageSharp Color Equals")]
    public bool ColorEqual()
        => new Rgba32(128, 128, 128, 128).Equals(new(128, 128, 128, 128));
}
