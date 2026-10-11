// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Fields;

namespace SixLabors.ImageSharp.Formats.Jxl.IO.Metadata;

internal sealed class JxlAnimationHeader : IJxlFields
{
    public int TpsNumerator { get; set; }

    public int TpsDenominator { get; set; }

    public int LoopCount { get; set; }

    public bool ContainsTimecodes { get; set; }

    public bool Visit(JxlVisitor visitor) => throw new NotImplementedException();
}
