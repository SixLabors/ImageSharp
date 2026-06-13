// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal enum Av1TransformSize : byte
{
    Size4x4 = 0,
    Size8x8 = 1,
    Size16x16 = 2,
    Size32x32 = 3,
    Size64x64 = 4,
    Size4x8 = 5,
    Size8x4 = 6,
    Size8x16 = 7,
    Size16x8 = 8,
    Size16x32 = 9,
    Size32x16 = 10,
    Size32x64 = 11,
    Size64x32 = 12,
    Size4x16 = 13,
    Size16x4 = 14,
    Size8x32 = 15,
    Size32x8 = 16,
    Size16x64 = 17,
    Size64x16 = 18,
    AllSizes = 19,
    SquareSizes = Size4x8,
    Invalid = 255,
}
