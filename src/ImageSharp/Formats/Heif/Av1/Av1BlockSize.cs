// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

internal enum Av1BlockSize
{
    Block4x4,
    Block4x8,
    Block8x4,
    Block8x8,
    Block8x16,
    Block16x8,
    Block16x16,
    Block16x32,
    Block32x16,
    Block32x32,
    Block32x64,
    Block64x32,
    Block64x64,
    Block64x128,
    Block128x64,
    Block128x128,
    Block4x16,
    Block16x4,
    Block8x32,
    Block32x8,
    Block16x64,
    Block64x16,
    BlockSizeSAll,
    BlockSizeS = Block4x16,
    BlockInvalid = 255,
    BlockLargest = BlockSizeS - 1,
}
