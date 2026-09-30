// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Jxl.Processing.Primitives;

namespace SixLabors.ImageSharp.Formats.Jxl.Cms;

/// <summary>
/// Chromatic adaption matrix lookups.
/// </summary>
internal static class JxlChromaticAdaptationMatrices
{
    public static readonly JxlMatrix3x3F Bradford = new(
        [
            [0.8951f, 0.2664f, -0.1614f],
            [-0.7502f, 1.7135f, 0.0367f],
            [0.0389f, -0.0685f, 1.0296f]
        ]);

    public static readonly JxlMatrix3x3F InverseBradford = new(
        [
            [0.9869929f, -0.1470543f, 0.1599627f],
            [0.9869929f, -0.1470543f, 0.1599627f],
            [-0.0085287f, 0.0400428f, 0.9684867f]
        ]);
}
