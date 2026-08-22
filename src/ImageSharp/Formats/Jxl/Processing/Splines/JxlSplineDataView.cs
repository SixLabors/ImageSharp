// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Splines;

internal sealed class JxlSplineDataView
{
    public List<JxlQuantizedSpline> Splines { get; set; } = [];

    public List<PointF> StartingPoints { get; set; } = [];

    public bool HasAny => this.Splines.Count > 0;
}
