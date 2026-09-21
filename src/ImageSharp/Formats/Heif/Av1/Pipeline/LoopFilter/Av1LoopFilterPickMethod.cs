// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopFilter;

/// <summary>
/// Selects how the encoder chooses the deblocking filter levels of a frame, as libaom's <c>LPF_PICK_METHOD</c> does.
/// </summary>
internal enum Av1LoopFilterPickMethod
{
    /// <summary>
    /// Searches the vertical and horizontal luma levels separately over the whole frame
    /// (<c>LPF_PICK_FROM_FULL_IMAGE</c>).
    /// </summary>
    FullImage = 0,

    /// <summary>
    /// Searches one luma level for both edge directions over the whole frame
    /// (<c>LPF_PICK_FROM_FULL_IMAGE_NON_DUAL</c>).
    /// </summary>
    FullImageNonDual = 1,

    /// <summary>
    /// Derives every level from the frame's AC quantizer without filtering trials (<c>LPF_PICK_FROM_Q</c>).
    /// </summary>
    FromQuantizer = 2,
}
