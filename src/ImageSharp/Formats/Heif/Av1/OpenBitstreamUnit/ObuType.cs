// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Identifies an AV1 open bitstream unit payload.
/// </summary>
internal enum ObuType
{
    /// <summary>
    /// The reserved zero value.
    /// </summary>
    None = 0,

    /// <summary>
    /// A sequence header.
    /// </summary>
    SequenceHeader = 1,

    /// <summary>
    /// A temporal delimiter.
    /// </summary>
    TemporalDelimiter = 2,

    /// <summary>
    /// A frame header without tile data.
    /// </summary>
    FrameHeader = 3,

    /// <summary>
    /// One or more encoded tiles.
    /// </summary>
    TileGroup = 4,

    /// <summary>
    /// Metadata associated with the coded sequence.
    /// </summary>
    Metadata = 5,

    /// <summary>
    /// A frame header followed by tile data.
    /// </summary>
    Frame = 6,

    /// <summary>
    /// A repeated copy of the current frame header.
    /// </summary>
    RedundantFrameHeader = 7,

    /// <summary>
    /// A list of tiles for large-scale tile decoding.
    /// </summary>
    TileList = 8,

    /// <summary>
    /// Padding bytes.
    /// </summary>
    Padding = 15,
}
