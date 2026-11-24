// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Ani;

/// <summary>
/// Flags for the ANI header.
/// </summary>
[Flags]
internal enum AniHeaderFlags : uint
{
    /// <summary>
    /// The "icon" chunks contain ICO or CUR resources. Without this flag, they contain raw bitmaps.
    /// </summary>
    IsIcon = 1,

    /// <summary>
    /// The ANI file contains a "seq " chunk that maps animation steps to frame resources.
    /// </summary>
    ContainsSequence = 2
}
