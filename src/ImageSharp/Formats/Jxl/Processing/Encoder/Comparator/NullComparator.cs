// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Comparator;

/// <summary>
/// Performs no image comparison at all.
/// </summary>
internal sealed class NullComparator : JxlComparator
{
    public override float GoodQualityScore => 0;

    public override float BadQualityScore => 0;

    public override void CompareWith(Configuration configuration, JxlImageBundle actual, JxlImageF? diffMap, out float score) => score = 0;

    public override void SetReferenceImage(Configuration configuration, JxlImageBundle reference)
    {
    }
}
