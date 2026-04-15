// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Exr.Constants;

/// <summary>
/// Enum for the different scan line ordering.
/// </summary>
internal enum ExrLineOrder : byte
{
    /// <summary>
    /// The scan lines are written from top-to-bottom.
    /// </summary>
    IncreasingY = 0,

    /// <summary>
    /// The scan lines are written from bottom-to-top.
    /// </summary>
    DecreasingY = 1,

    /// <summary>
    /// The Scan lines are written in no particular oder.
    /// </summary>
    RandomY = 2
}
