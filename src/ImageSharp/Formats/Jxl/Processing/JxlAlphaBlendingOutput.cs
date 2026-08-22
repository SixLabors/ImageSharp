// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

internal sealed class JxlAlphaBlendingOutput
{
    public Memory<float> R { get; set; }

    public Memory<float> G { get; set; }

    public Memory<float> B { get; set; }

    public Memory<float> A { get; set; }
}
