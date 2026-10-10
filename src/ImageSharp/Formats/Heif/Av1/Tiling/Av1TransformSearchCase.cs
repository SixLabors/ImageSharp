// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Names the inter searches that have a separate transform search level.
/// </summary>
internal enum Av1TransformSearchCase
{
    /// <summary>
    /// A search that uses the default transform search level.
    /// </summary>
    Default = 0,

    /// <summary>
    /// The motion-mode search of a block larger than 16x16 inside the mode loop.
    /// </summary>
    MotionMode = 1,

    /// <summary>
    /// The compound-type search when masked compound is enabled.
    /// </summary>
    CompoundType = 2,
}
