// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal enum Av1TransformSize : byte
{
    Size4x4,
    Size8x8,
    Size16x16,
    Size32x32,
    Size64x64,
    Size4x8,
    Size8x4,
    Size8x16,
    Size16x8,
    Size16x32,
    Size32x16,
    Size32x64,
    Size64x32,
    Size4x16,
    Size16x4,
    Size8x32,
    Size32x8,
    Size16x64,
    Size64x16,
    AllSizes,
    SquareSizes = Size4x8,
    Invalid = 255,
}
