// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Identifies every square and rectangular transform-block size defined by AV1.
/// </summary>
internal enum Av1TransformSize : byte
{
    /// <summary>
    /// A 4-by-4 transform block.
    /// </summary>
    Size4x4 = 0,

    /// <summary>
    /// An 8-by-8 transform block.
    /// </summary>
    Size8x8 = 1,

    /// <summary>
    /// A 16-by-16 transform block.
    /// </summary>
    Size16x16 = 2,

    /// <summary>
    /// A 32-by-32 transform block.
    /// </summary>
    Size32x32 = 3,

    /// <summary>
    /// A 64-by-64 transform block.
    /// </summary>
    Size64x64 = 4,

    /// <summary>
    /// A 4-by-8 transform block.
    /// </summary>
    Size4x8 = 5,

    /// <summary>
    /// An 8-by-4 transform block.
    /// </summary>
    Size8x4 = 6,

    /// <summary>
    /// An 8-by-16 transform block.
    /// </summary>
    Size8x16 = 7,

    /// <summary>
    /// A 16-by-8 transform block.
    /// </summary>
    Size16x8 = 8,

    /// <summary>
    /// A 16-by-32 transform block.
    /// </summary>
    Size16x32 = 9,

    /// <summary>
    /// A 32-by-16 transform block.
    /// </summary>
    Size32x16 = 10,

    /// <summary>
    /// A 32-by-64 transform block.
    /// </summary>
    Size32x64 = 11,

    /// <summary>
    /// A 64-by-32 transform block.
    /// </summary>
    Size64x32 = 12,

    /// <summary>
    /// A 4-by-16 transform block.
    /// </summary>
    Size4x16 = 13,

    /// <summary>
    /// A 16-by-4 transform block.
    /// </summary>
    Size16x4 = 14,

    /// <summary>
    /// An 8-by-32 transform block.
    /// </summary>
    Size8x32 = 15,

    /// <summary>
    /// A 32-by-8 transform block.
    /// </summary>
    Size32x8 = 16,

    /// <summary>
    /// A 16-by-64 transform block.
    /// </summary>
    Size16x64 = 17,

    /// <summary>
    /// A 64-by-16 transform block.
    /// </summary>
    Size64x16 = 18,

    /// <summary>
    /// The number of defined transform-block sizes.
    /// </summary>
    AllSizes = 19,

    /// <summary>
    /// The number of square transform-block sizes.
    /// </summary>
    SquareSizes = Size4x8,

    /// <summary>
    /// No valid transform-block size.
    /// </summary>
    Invalid = 255,
}
