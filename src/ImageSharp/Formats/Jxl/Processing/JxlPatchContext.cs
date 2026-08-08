// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing;

/// <summary>
/// Context numbers for patch decoding
/// </summary>
internal enum JxlPatchContext : byte
{
    NumRefPatch = 0,

    ReferenceFrame = 1,

    PatchSize = 2,

    PatchReferencePosition = 3,

    PatchPosition = 4,

    PatchBlendMode = 5,

    PatchOffset = 6,

    PatchCount = 7,

    PatchAlphaChannel = 8,

    PatchClamp = 9,

    NumPatchDictionaryContexts
}
