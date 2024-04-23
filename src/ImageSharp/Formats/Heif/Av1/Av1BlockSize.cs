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

internal static class Av1BlockSizeExtensions
{
    private static readonly int[] SizeWide = { 1, 1, 2, 2, 2, 4, 4, 4, 8, 8, 8, 16, 16, 16, 32, 32, 1, 4, 2, 8, 4, 16 };
    private static readonly int[] SizeHigh = { 1, 2, 1, 2, 4, 2, 4, 8, 4, 8, 16, 8, 16, 32, 16, 32, 4, 1, 8, 2, 16, 4 };

    public static int Get4x4WideCount(this Av1BlockSize blockSize) => SizeWide[(int)blockSize];

    public static int Get4x4HighCount(this Av1BlockSize blockSize) => SizeHigh[(int)blockSize];
}
