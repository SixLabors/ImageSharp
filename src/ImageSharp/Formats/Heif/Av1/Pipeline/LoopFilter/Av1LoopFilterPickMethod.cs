// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopFilter;

/// <summary>
/// Selects how the encoder chooses the deblocking filter levels of a frame.
/// </summary>
internal enum Av1LoopFilterPickMethod
{
    /// <summary>
    /// Searches the vertical and horizontal luma levels separately over the whole frame.
    /// </summary>
    FullImage = 0,

    /// <summary>
    /// Searches one luma level for both edge directions over the whole frame.
    /// </summary>
    FullImageNonDual = 1,

    /// <summary>
    /// Derives every level from the frame's AC quantizer without filtering trials.
    /// </summary>
    FromQuantizer = 2,
}
