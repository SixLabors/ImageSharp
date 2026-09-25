// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Names the inter searches that gate their transform search separately. Reference: TX_SEARCH_CASE.
/// </summary>
internal enum Av1TransformSearchCase
{
    /// <summary>
    /// A search without its own level. Reference: TX_SEARCH_DEFAULT.
    /// </summary>
    Default = 0,

    /// <summary>
    /// The motion-mode search of a block larger than 16x16 inside the mode loop. Reference: TX_SEARCH_MOTION_MODE.
    /// </summary>
    MotionMode = 1,

    /// <summary>
    /// The compound-type search when masked compound is enabled. Reference: TX_SEARCH_COMP_TYPE_MODE.
    /// </summary>
    CompoundType = 2,
}
