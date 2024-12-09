// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

// Generates 5 bit field in which each bit set to 1 represents
// a BlockSize partition  11111 means we split 128x128, 64x64, 32x32, 16x16
// and 8x8.  10000 means we just split the 128x128 to 64x64
internal struct Av1PartitionContext : IMinMaxValue<Av1PartitionContext>
{
    private static readonly int[] AboveLookup =
        [31, 31, 30, 30, 30, 28, 28, 28, 24, 24, 24, 16, 16, 16, 0, 0, 31, 28, 30, 24, 28, 16];

    private static readonly int[] LeftLookup =
        [31, 30, 31, 30, 28, 30, 28, 24, 28, 24, 16, 24, 16, 0, 16, 0, 28, 31, 24, 30, 16, 28];

    // Mask to extract ModeInfo offset within max ModeInfoBlock
    public const int Mask = (1 << (7 - 2)) - 1;

    public static Av1PartitionContext MaxValue => throw new NotImplementedException();

    public static Av1PartitionContext MinValue => throw new NotImplementedException();

    public byte Left { get; internal set; }

    public byte Above { get; internal set; }

    public static int GetAboveContext(Av1BlockSize blockSize) => AboveLookup[(int)blockSize];

    public static int GetLeftContext(Av1BlockSize blockSize) => LeftLookup[(int)blockSize];
}
