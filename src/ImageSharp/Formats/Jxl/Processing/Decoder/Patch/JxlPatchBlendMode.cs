// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder.Patch;

internal enum JxlPatchBlendMode : byte
{
    None,
    Replace,
    Add,
    Multiply,
    BlendAbove,
    BlendBelow,
    AlphaWeightedAddAbove,
    AlphaWeightedAddBelow
}
