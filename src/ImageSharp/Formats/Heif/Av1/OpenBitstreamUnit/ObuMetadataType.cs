// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Identifies the payload carried by an AV1 metadata OBU.
/// </summary>
internal enum ObuMetadataType
{
    /// <summary>
    /// The reserved zero value.
    /// </summary>
    Reserved = 0,

    /// <summary>
    /// Content light-level metadata.
    /// </summary>
    HdrCll = 1,

    /// <summary>
    /// Mastering-display color-volume metadata.
    /// </summary>
    HdrMdcv = 2,

    /// <summary>
    /// Scalability-structure metadata.
    /// </summary>
    Scalability = 3,

    /// <summary>
    /// ITU-T T.35 terminal-provider metadata.
    /// </summary>
    ItutT35 = 4,

    /// <summary>
    /// Timecode metadata.
    /// </summary>
    Timecode = 5,
}
