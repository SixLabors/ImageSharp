// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Describes one retained coded sample in a HEIF image sequence.
/// </summary>
internal struct HeifSequenceSample
{
    /// <summary>
    /// Gets or sets the absolute file offset of the coded sample.
    /// </summary>
    public long Offset { get; set; }

    /// <summary>
    /// Gets or sets the coded sample length in bytes.
    /// </summary>
    public int Length { get; set; }

    /// <summary>
    /// Gets or sets the sample duration in media-time-scale units.
    /// </summary>
    public uint Duration { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether decoding can begin at this sample.
    /// </summary>
    public bool IsSync { get; set; }
}
