// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Blending;

internal ref struct JxlAlphaBlendingOutput(Span<float> singleSpan)
{
    public Span<float> R = singleSpan;

    public Span<float> G = singleSpan;

    public Span<float> B = singleSpan;

    public Span<float> A = singleSpan;
}
