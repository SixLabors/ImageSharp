// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

internal sealed class JxlNoiseParameters
{
    public const int NoisePoints = 8;

    public float[] Lookup { get; set; } = new float[NoisePoints];

    public bool ContainsAny => this.Lookup.Any(x => MathF.Abs(x) > 1e-3f);

    public void Clear() => Array.Fill(this.Lookup, 0f);
}
