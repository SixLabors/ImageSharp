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
    /// Gets or sets the signed offset from decode time to composition time in media-time-scale units.
    /// </summary>
    public long CompositionOffset { get; set; }

    /// <summary>
    /// Gets or sets the computed composition time in media-time-scale units.
    /// </summary>
    public long CompositionTime { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the sample is decoded only as a reference and is not presented.
    /// </summary>
    public bool IsHidden { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether decoding can begin at this sample.
    /// </summary>
    public bool IsSync { get; set; }

    /// <summary>
    /// Gets or sets the positive identifier used when another retained sample directly references this sample.
    /// </summary>
    public uint SampleId { get; set; }

    /// <summary>
    /// Gets or sets the first direct reference in the owning track's compact reference-index array.
    /// </summary>
    public int DirectReferenceOffset { get; set; }

    /// <summary>
    /// Gets or sets the number of direct reference indices belonging to this sample.
    /// </summary>
    public byte DirectReferenceCount { get; set; }
}
