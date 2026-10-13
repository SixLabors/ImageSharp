// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Identifies the current or retained frame used to predict an AV1 coding block.
/// </summary>
internal enum Av1ReferenceFrameType : sbyte
{
    /// <summary>
    /// Indicates that the optional secondary reference is absent.
    /// </summary>
    None = -1,

    /// <summary>
    /// References the current frame for intra prediction.
    /// </summary>
    Intra = 0,

    /// <summary>
    /// References the most recent forward prediction frame.
    /// </summary>
    Last = 1,

    /// <summary>
    /// References the second most recent forward prediction frame.
    /// </summary>
    Last2 = 2,

    /// <summary>
    /// References the third most recent forward prediction frame.
    /// </summary>
    Last3 = 3,

    /// <summary>
    /// References the long-term golden forward prediction frame.
    /// </summary>
    Golden = 4,

    /// <summary>
    /// References the nearest backward prediction frame.
    /// </summary>
    Backward = 5,

    /// <summary>
    /// References the secondary alternate backward prediction frame.
    /// </summary>
    Alternate2 = 6,

    /// <summary>
    /// References the alternate backward prediction frame.
    /// </summary>
    Alternate = 7,
}
