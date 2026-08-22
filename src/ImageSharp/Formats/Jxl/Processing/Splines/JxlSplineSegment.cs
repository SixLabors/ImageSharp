// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Splines;

internal struct JxlSplineSegment
{
    public PointF Center { get; set; }

    public float MaximumDistance { get; set; }

    public float InverseSigma { get; set; }

    public float SigmaOver4TimesIntensity { get; set; }

    public InlineArray3<float> Color { get; set; }
}
