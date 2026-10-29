// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Splines;

/// <summary>
/// Identifies the image rows touched by one cached spline segment.
/// </summary>
/// <param name="startInclusive">The first row that can contain samples.</param>
/// <param name="endExclusive">The row immediately after the affected range.</param>
internal struct JxlSplineSegmentSpan(int startInclusive, int endExclusive)
{
    /// <summary>
    /// Gets or sets the first affected row.
    /// </summary>
    public int StartInclusive { get; set; } = startInclusive;

    /// <summary>
    /// Gets or sets the row immediately after the affected range.
    /// </summary>
    public int EndExclusive { get; set; } = endExclusive;
}
