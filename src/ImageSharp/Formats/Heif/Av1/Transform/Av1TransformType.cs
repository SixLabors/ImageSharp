// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Identifies the horizontal and vertical transform combination signaled for an AV1 transform block.
/// </summary>
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

    /// <summary>
    /// Flipped ADST vertically and DCT horizontally.
    /// </summary>
    FlipAdstDct,

    /// <summary>
    /// DCT vertically and flipped ADST horizontally.
    /// </summary>
    DctFlipAdst,

    /// <summary>
    /// Flipped ADST in both directions.
    /// </summary>
    FlipAdstFlipAdst,

    /// <summary>
    /// ADST vertically and flipped ADST horizontally.
    /// </summary>
    AdstFlipAdst,

    /// <summary>
    /// Flipped ADST vertically and ADST horizontally.
    /// </summary>
    FlipAdstAdst,

    /// <summary>
    /// Identity transforms in both directions.
    /// </summary>
    Identity,

    /// <summary>
    /// DCT vertically and identity horizontally.
    /// </summary>
    VerticalDct,

    /// <summary>
    /// Identity vertically and DCT horizontally.
    /// </summary>
    HorizontalDct,

    /// <summary>
    /// ADST vertically and identity horizontally.
    /// </summary>
    VerticalAdst,

    /// <summary>
    /// Identity vertically and ADST horizontally.
    /// </summary>
    HorizontalAdst,

    /// <summary>
    /// Flipped ADST vertically and identity horizontally.
    /// </summary>
    VerticalFlipAdst,

    /// <summary>
    /// Identity vertically and flipped ADST horizontally.
    /// </summary>
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
