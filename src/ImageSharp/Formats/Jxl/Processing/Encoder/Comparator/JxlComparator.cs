// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Memory.ImageTypes;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Image;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Comparator;

/// <summary>
/// Compares images.
/// </summary>
internal abstract class JxlComparator
{
    public abstract float GoodQualityScore { get; }

    public abstract float BadQualityScore { get; }

    // Sets the reference image, the first to compare.
    public abstract void SetReferenceImage(Configuration configuration, JxlImageBundle reference);

    // Sets the actual image, the second to compare.
    public abstract void CompareWith(
        Configuration configuration,
        JxlImageBundle actual,
        JxlImageF? diffMap,
        out float score);
}
