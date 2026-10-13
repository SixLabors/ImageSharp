// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Identifies the position of chroma samples relative to luma samples.
/// </summary>
internal enum ObuChromoSamplePosition : byte
{
    /// <summary>
    /// Unknown.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The chroma sample is horizontally co-located with the top-left luma sample and lies between two luma rows.
    /// </summary>
    Vertical = 1,

    /// <summary>
    /// The chroma sample is co-located with the top-left luma sample.
    /// </summary>
    Colocated = 2,

    /// <summary>
    /// Reserved and invalid for AV1 content.
    /// </summary>
    Reserved = 3,
}
