// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Blending;

internal ref struct JxlAlphaBlendingInputLayer(ReadOnlySpan<float> singleSpan)
{
    public ReadOnlySpan<float> R = singleSpan;

    public ReadOnlySpan<float> G = singleSpan;

    public ReadOnlySpan<float> B = singleSpan;

    public ReadOnlySpan<float> A = singleSpan;
}
