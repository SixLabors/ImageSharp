// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Splines;

internal struct JxlSplineSegmentSpan(int startInclusive, int endInclusive)
{
    public int StartInclusive { get; set; } = startInclusive;

    public int EndInclusive { get; set; } = endInclusive;
}
