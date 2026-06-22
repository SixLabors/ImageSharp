// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal enum Av1TransformType : byte
{
    /// <summary>
    /// DCT in both horizontal and vertical.
    /// </summary>
    DctDct,

    /// <summary>
    /// ADST in vertical, DCT in horizontal.
    /// </summary>
    AdstDct,

    /// <summary>
    /// DCT in vertical, ADST in horizontal.
    /// </summary>
    DctAdst,

    /// <summary>
    /// ADST in both directions.
    /// </summary>
    AdstAdst,
    FlipAdstDct,
    DctFlipAdst,
    FlipAdstFlipAdst,
    AdstFlipAdst,
    FlipAdstAdst,
    Identity,
    VerticalDct,
    HorizontalDct,
    VerticalAdst,
    HorizontalAdst,
    VerticalFlipAdst,
    HorizontalFlipAdst,

    /// <summary>
    /// Number of Transform types.
    /// </summary>
    AllTransformTypes,

    /// <summary>
    /// Invalid value.
    /// </summary>
    Invalid,
}
