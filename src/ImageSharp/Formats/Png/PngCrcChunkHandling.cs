// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Png;

/// <summary>
/// Specifies how to handle validation of any CRC (Cyclic Redundancy Check) data within the encoded PNG.
/// </summary>
public enum PngCrcChunkHandling
{
    /// <summary>
    /// Do not ignore any CRC chunk errors.
    /// </summary>
    IgnoreNone,

    /// <summary>
    /// Ignore CRC errors in non critical chunks.
    /// </summary>
    IgnoreNonCritical,

    /// <summary>
    /// Ignore CRC errors in data chunks.
    /// </summary>
    IgnoreData,

    /// <summary>
    /// Ignore CRC errors in all chunks.
    /// </summary>
    IgnoreAll
}
