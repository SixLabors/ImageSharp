// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

internal struct JxlWeightsSeparable5
{
    // Don't make these a property so we can ref into them.

    public InlineArray12<float> Horizontal;

    public InlineArray12<float> Vertical;
}
