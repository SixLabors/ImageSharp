// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp.Lossy;

/// <summary>
/// The segment features of a VP8 frame.
/// </summary>
/// <remarks>
/// See RFC 6386, section 9.3 "Segment-Based Adjustments": https://datatracker.ietf.org/doc/html/rfc6386#section-9.3.
/// </remarks>
internal class Vp8SegmentHeader
{
    private const int NumMbSegments = 4;

    /// <summary>
    /// Initializes a new instance of the <see cref="Vp8SegmentHeader"/> class.
    /// </summary>
    public Vp8SegmentHeader()
    {
        this.Quantizer = new sbyte[NumMbSegments];
        this.FilterStrength = new sbyte[NumMbSegments];
    }

    /// <summary>
    /// Gets or sets a value indicating whether the frame uses segments.
    /// </summary>
    public bool UseSegment { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to update the segment map or not.
    /// </summary>
    public bool UpdateMap { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the segment quantizer and filter values are absolute values. If the value is <see langword="false"/>, each value
    /// is a delta to the frame value.
    /// </summary>
    /// <remarks>
    /// A frame that does not update the segment data keeps the absolute mode, as libwebp does.
    /// </remarks>
    public bool AbsoluteDelta { get; set; } = true;

    /// <summary>
    /// Gets the quantizer of each segment. The 7-bit value has a sign, so a delta can be negative.
    /// </summary>
    public sbyte[] Quantizer { get; }

    /// <summary>
    /// Gets the filter strength of each segment. The 6-bit value has a sign, so a delta can be negative.
    /// </summary>
    public sbyte[] FilterStrength { get; }
}
