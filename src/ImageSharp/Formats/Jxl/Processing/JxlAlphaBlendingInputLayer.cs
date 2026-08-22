// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

internal sealed class JxlAlphaBlendingInputLayer
{
    public ReadOnlyMemory<float> R { get; set; }

    public ReadOnlyMemory<float> G { get; set; }

    public ReadOnlyMemory<float> B { get; set; }

    public ReadOnlyMemory<float> A { get; set; }
}
