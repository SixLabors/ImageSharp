// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Carries the neighboring coefficient contexts used to entropy-code an AV1 transform block.
/// </summary>
internal class Av1TransformBlockContext
{
    /// <summary>
    /// Gets or sets the context used to decode the sign of the DC coefficient.
    /// </summary>
    public int DcSignContext { get; set; }

    /// <summary>
    /// Gets or sets the neighboring transform-block skip context.
    /// </summary>
    public int SkipContext { get; set; }
}
