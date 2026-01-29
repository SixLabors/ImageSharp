// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal class Av1BottomRightTopLeftConstants
{
    // Tables to store if the top-right reference pixels are available. The flags
    // are represented with bits, packed into 8-bit integers. E.g., for the 32x32
    // blocks in a 128x128 superblock, the index of the "o" block is 10 (in raster
    // order), so its flag is stored at the 3rd bit of the 2nd entry in the table,
    // i.e. (table[10 / 8] >> (10 % 8)) & 1.
    //       . . . .
    //       . . . .
    //       . . o .
    //       . . . .
    private static readonly byte[] HasTopRight4x4 = [
        255, 255, 255, 255, 85,  85,  85,  85,  119, 119, 119, 119, 85,  85,  85,  85,  127, 127, 127, 127, 85,  85,
        85,  85,  119, 119, 119, 119, 85,  85,  85,  85,  255, 127, 255, 127, 85,  85,  85,  85,  119, 119, 119, 119,
        85,  85,  85,  85,  127, 127, 127, 127, 85,  85,  85,  85,  119, 119, 119, 119, 85,  85,  85,  85,  255, 255,
        255, 127, 85,  85,  85,  85,  119, 119, 119, 119, 85,  85,  85,  85,  127, 127, 127, 127, 85,  85,  85,  85,
        119, 119, 119, 119, 85,  85,  85,  85,  255, 127, 255, 127, 85,  85,  85,  85,  119, 119, 119, 119, 85,  85,
        85,  85,  127, 127, 127, 127, 85,  85,  85,  85,  119, 119, 119, 119, 85,  85,  85,  85,
    ];

    private static readonly byte[] HasTopRight4x8 = [
        255, 255, 255, 255, 119, 119, 119, 119, 127, 127, 127, 127, 119, 119, 119, 119, 255, 127, 255, 127, 119, 119,
        119, 119, 127, 127, 127, 127, 119, 119, 119, 119, 255, 255, 255, 127, 119, 119, 119, 119, 127, 127, 127, 127,
        119, 119, 119, 119, 255, 127, 255, 127, 119, 119, 119, 119, 127, 127, 127, 127, 119, 119, 119, 119,
    ];

    private static readonly byte[] HasTopRight8x4 = [
        255, 255, 0,   0,   85,  85,  0,  0,  119, 119, 0,   0,   85,  85,  0,  0,  127, 127, 0,   0,   85, 85,
        0,   0,   119, 119, 0,   0,   85, 85, 0,   0,   255, 127, 0,   0,   85, 85, 0,   0,   119, 119, 0,  0,
        85,  85,  0,   0,   127, 127, 0,  0,  85,  85,  0,   0,   119, 119, 0,  0,  85,  85,  0,   0,
    ];

    private static readonly byte[] HasTopRight8x8 = [
        255, 255, 85, 85, 119, 119, 85, 85, 127, 127, 85, 85, 119, 119, 85, 85,
        255, 127, 85, 85, 119, 119, 85, 85, 127, 127, 85, 85, 119, 119, 85, 85,
    ];

    private static readonly byte[] HasTopRight8x16 = [
        255,
        255,
        119,
        119,
        127,
        127,
        119,
        119,
        255,
        127,
        119,
        119,
        127,
        127,
        119,
        119,
    ];

    private static readonly byte[] HasTopRight16x8 = [
        255,
        0,
        85,
        0,
        119,
        0,
        85,
        0,
        127,
        0,
        85,
        0,
        119,
        0,
        85,
        0,
    ];

    private static readonly byte[] HasTopRight16x16 = [
        255,
        85,
        119,
        85,
        127,
        85,
        119,
        85,
    ];

    private static readonly byte[] HasTopRight16x32 = [255, 119, 127, 119];
    private static readonly byte[] HasTopRight32x16 = [15, 5, 7, 5];
    private static readonly byte[] HasTopRight32x32 = [95, 87];
    private static readonly byte[] HasTopRight32x64 = [127];
    private static readonly byte[] HasTopRight64x32 = [19];
    private static readonly byte[] HasTopRight64x64 = [7];
    private static readonly byte[] HasTopRight64x128 = [3];
    private static readonly byte[] HasTopRight128x64 = [1];
    private static readonly byte[] HasTopRight128x128 = [1];
    private static readonly byte[] HasTopRight4x16 = [
        255, 255, 255, 255, 127, 127, 127, 127, 255, 127, 255, 127, 127, 127, 127, 127,
        255, 255, 255, 127, 127, 127, 127, 127, 255, 127, 255, 127, 127, 127, 127, 127,
    ];

    private static readonly byte[] HasTopRight16x4 = [
        255, 0, 0, 0, 85, 0, 0, 0, 119, 0, 0, 0, 85, 0, 0, 0, 127, 0, 0, 0, 85, 0, 0, 0, 119, 0, 0, 0, 85, 0, 0, 0,
    ];

    private static readonly byte[] HasTopRight8x32 = [
        255,
        255,
        127,
        127,
        255,
        127,
        127,
        127,
    ];

    private static readonly byte[] HasTopRight32x8 = [
        15,
        0,
        5,
        0,
        7,
        0,
        5,
        0,
    ];

    private static readonly byte[] HasTopRight16x64 = [255, 127];
    private static readonly byte[] HasTopRight64x16 = [3, 1];

    private static readonly byte[][] HasTopRightTables = [

        // 4X4
        HasTopRight4x4,

        // 4X8,       8X4,            8X8
        HasTopRight4x8,
        HasTopRight8x4,
        HasTopRight8x8,

        // 8X16,      16X8,           16X16
        HasTopRight8x16,
        HasTopRight16x8,
        HasTopRight16x16,

        // 16X32,     32X16,          32X32
        HasTopRight16x32,
        HasTopRight32x16,
        HasTopRight32x32,

        // 32X64,     64X32,          64X64
        HasTopRight32x64,
        HasTopRight64x32,
        HasTopRight64x64,

        // 64x128,    128x64,         128x128
        HasTopRight64x128,
        HasTopRight128x64,
        HasTopRight128x128,

        // 4x16,      16x4,            8x32
        HasTopRight4x16,
        HasTopRight16x4,
        HasTopRight8x32,

        // 32x8,      16x64,           64x16
        HasTopRight32x8,
        HasTopRight16x64,
        HasTopRight64x16
    ];

    private static readonly byte[] HasTopRightVertical8x8 = [
        255, 255, 0, 0, 119, 119, 0, 0, 127, 127, 0, 0, 119, 119, 0, 0,
        255, 127, 0, 0, 119, 119, 0, 0, 127, 127, 0, 0, 119, 119, 0, 0,
    ];

    private static readonly byte[] HasTopRightVertical16x16 = [
        255,
        0,
        119,
        0,
        127,
        0,
        119,
        0,
    ];

    private static readonly byte[] HasTopRightVertical32x32 = [15, 7];
    private static readonly byte[] HasTopRightVertical64x64 = [3];

    // The _vert_* tables are like the ordinary tables above, but describe the
    // order we visit square blocks when doing a PARTITION_VERT_A or
    // PARTITION_VERT_B. This is the same order as normal except for on the last
    // split where we go vertically (TL, BL, TR, BR). We treat the rectangular block
    // as a pair of squares, which means that these tables work correctly for both
    // mixed vertical partition types.
    //
    // There are tables for each of the square sizes. Vertical rectangles (like
    // BLOCK_16X32) use their respective "non-vert" table
    private static readonly byte[]?[] HasTopRightVerticalTables = [

        // 4X4
        null,

        // 4X8,      8X4,         8X8
        HasTopRight4x8,
        null,
        HasTopRightVertical8x8,

        // 8X16,     16X8,        16X16
        HasTopRight8x16,
        null,
        HasTopRightVertical16x16,

        // 16X32,    32X16,       32X32
        HasTopRight16x32,
        null,
        HasTopRightVertical32x32,

        // 32X64,    64X32,       64X64
        HasTopRight32x64,
        null,
        HasTopRightVertical64x64,

        // 64x128,   128x64,      128x128
        HasTopRight64x128,
        null,
        HasTopRight128x128
    ];

    // Similar to the has_tr_* tables, but store if the bottom-left reference
    // pixels are available.
    private static readonly byte[] HasBottomLeft4x4 = [
        84, 85, 85, 85, 16, 17, 17, 17, 84, 85, 85, 85, 0,  1,  1,  1,  84, 85, 85, 85, 16, 17, 17, 17, 84, 85,
        85, 85, 0,  0,  1,  0,  84, 85, 85, 85, 16, 17, 17, 17, 84, 85, 85, 85, 0,  1,  1,  1,  84, 85, 85, 85,
        16, 17, 17, 17, 84, 85, 85, 85, 0,  0,  0,  0,  84, 85, 85, 85, 16, 17, 17, 17, 84, 85, 85, 85, 0,  1,
        1,  1,  84, 85, 85, 85, 16, 17, 17, 17, 84, 85, 85, 85, 0,  0,  1,  0,  84, 85, 85, 85, 16, 17, 17, 17,
        84, 85, 85, 85, 0,  1,  1,  1,  84, 85, 85, 85, 16, 17, 17, 17, 84, 85, 85, 85, 0,  0,  0,  0,
    ];

    private static readonly byte[] HasBottomLeft4x8 = [
        16, 17, 17, 17, 0, 1, 1, 1, 16, 17, 17, 17, 0, 0, 1, 0, 16, 17, 17, 17, 0, 1, 1, 1, 16, 17, 17, 17, 0, 0, 0, 0,
        16, 17, 17, 17, 0, 1, 1, 1, 16, 17, 17, 17, 0, 0, 1, 0, 16, 17, 17, 17, 0, 1, 1, 1, 16, 17, 17, 17, 0, 0, 0, 0,
    ];

    private static readonly byte[] HasBottomLeft8x4 = [
        254, 255, 84,  85,  254, 255, 16,  17,  254, 255, 84,  85,  254, 255, 0,   1,   254, 255, 84,  85,  254, 255,
        16,  17,  254, 255, 84,  85,  254, 255, 0,   0,   254, 255, 84,  85,  254, 255, 16,  17,  254, 255, 84,  85,
        254, 255, 0,   1,   254, 255, 84,  85,  254, 255, 16,  17,  254, 255, 84,  85,  254, 255, 0,   0,
    ];

    private static readonly byte[] HasBottomLeft8x8 = [
        84, 85, 16, 17, 84, 85, 0, 1, 84, 85, 16, 17, 84, 85, 0, 0,
        84, 85, 16, 17, 84, 85, 0, 1, 84, 85, 16, 17, 84, 85, 0, 0,
    ];

    private static readonly byte[] HasBottomLeft8x16 = [
        16,
        17,
        0,
        1,
        16,
        17,
        0,
        0,
        16,
        17,
        0,
        1,
        16,
        17,
        0,
        0,
    ];

    private static readonly byte[] HasBottomLeft16x8 = [
        254,
        84,
        254,
        16,
        254,
        84,
        254,
        0,
        254,
        84,
        254,
        16,
        254,
        84,
        254,
        0,
    ];

    private static readonly byte[] HasBottomLeft16x16 = [
        84,
        16,
        84,
        0,
        84,
        16,
        84,
        0,
    ];

    private static readonly byte[] HasBottomLeft16x32 = [16, 0, 16, 0];
    private static readonly byte[] HasBottomLeft32x16 = [78, 14, 78, 14];
    private static readonly byte[] HasBottomLeft32x32 = [4, 4];
    private static readonly byte[] HasBottomLeft32x64 = [0];
    private static readonly byte[] HasBottomLeft64x32 = [34];
    private static readonly byte[] HasBottomLeft64x64 = [0];
    private static readonly byte[] HasBottomLeft64x128 = [0];
    private static readonly byte[] HasBottomLeft128x64 = [0];
    private static readonly byte[] HasBottomLeft128x128 = [0];
    private static readonly byte[] HasBottomLeft4x16 = [
        0, 1, 1, 1, 0, 0, 1, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 1, 0, 0, 1, 1, 1, 0, 0, 0, 0,
    ];

    private static readonly byte[] HasBottomLeft16x4 = [
        254, 254, 254, 84, 254, 254, 254, 16, 254, 254, 254, 84, 254, 254, 254, 0,
        254, 254, 254, 84, 254, 254, 254, 16, 254, 254, 254, 84, 254, 254, 254, 0,
    ];

    private static readonly byte[] HasBottomLeft8x32 = [
        0,
        1,
        0,
        0,
        0,
        1,
        0,
        0,
    ];

    private static readonly byte[] HasBottomLeft32x8 = [
        238,
        78,
        238,
        14,
        238,
        78,
        238,
        14,
    ];

    private static readonly byte[] HasBottomLeft16x64 = [0, 0];
    private static readonly byte[] HasBottomLeft64x16 = [42, 42];

    private static readonly byte[][] HasBottomLeftTables = [

        // 4X4
        HasBottomLeft4x4,

        // 4X8,         8X4,         8X8
        HasBottomLeft4x8,
        HasBottomLeft8x4,
        HasBottomLeft8x8,

        // 8X16,        16X8,        16X16
        HasBottomLeft8x16,
        HasBottomLeft16x8,
        HasBottomLeft16x16,

        // 16X32,       32X16,       32X32
        HasBottomLeft16x32,
        HasBottomLeft32x16,
        HasBottomLeft32x32,

        // 32X64,       64X32,       64X64
        HasBottomLeft32x64,
        HasBottomLeft64x32,
        HasBottomLeft64x64,

        // 64x128,      128x64,      128x128
        HasBottomLeft64x128,
        HasBottomLeft128x64,
        HasBottomLeft128x128,

        // 4x16,        16x4,        8x32
        HasBottomLeft4x16,
        HasBottomLeft16x4,
        HasBottomLeft8x32,

        // 32x8,        16x64,       64x16
        HasBottomLeft32x8,
        HasBottomLeft16x64,
        HasBottomLeft64x16
    ];

    private static readonly byte[] HasBottomLeftVertical8x8 = [
        254, 255, 16, 17, 254, 255, 0, 1, 254, 255, 16, 17, 254, 255, 0, 0,
        254, 255, 16, 17, 254, 255, 0, 1, 254, 255, 16, 17, 254, 255, 0, 0,
    ];

    private static readonly byte[] HasBottomLeftVertical16x16 = [
        254,
        16,
        254,
        0,
        254,
        16,
        254,
        0,
    ];

    private static readonly byte[] HasBottomLeftVertical32x32 = [14, 14];
    private static readonly byte[] HasBottomLeftVertical64x64 = [2];

    // The _vert_* tables are like the ordinary tables above, but describe the
    // order we visit square blocks when doing a PARTITION_VERT_A or
    // PARTITION_VERT_B. This is the same order as normal except for on the last
    // split where we go vertically (TL, BL, TR, BR). We treat the rectangular block
    // as a pair of squares, which means that these tables work correctly for both
    // mixed vertical partition types.
    //
    // There are tables for each of the square sizes. Vertical rectangles (like
    // BLOCK_16X32) use their respective "non-vert" table
    private static readonly byte[]?[] HasBottomLeftVerticalTables = [

        // 4X4
        null,

        // 4X8,     8X4,         8X8
        HasBottomLeft4x8,
        null,
        HasBottomLeftVertical8x8,

        // 8X16,    16X8,        16X16
        HasBottomLeft8x16,
        null,
        HasBottomLeftVertical16x16,

        // 16X32,   32X16,       32X32
        HasBottomLeft16x32,
        null,
        HasBottomLeftVertical32x32,

        // 32X64,   64X32,       64X64
        HasBottomLeft32x64,
        null,
        HasBottomLeftVertical64x64,

        // 64x128,  128x64,      128x128
        HasBottomLeft64x128,
        null,
        HasBottomLeft128x128];

    public static bool HasTopRight(Av1PartitionType partitionType, Av1BlockSize blockSize, int blockIndex)
    {
        int index1 = blockIndex / 8;
        int index2 = blockIndex % 8;
        Span<byte> hasBottomLeftTable = GetHasTopRightTable(partitionType, blockSize);
        return ((hasBottomLeftTable[index1] >> index2) & 1) > 0;
    }

    public static bool HasBottomLeft(Av1PartitionType partitionType, Av1BlockSize blockSize, int blockIndex)
    {
        int index1 = blockIndex / 8;
        int index2 = blockIndex % 8;
        Span<byte> hasBottomLeftTable = GetHasBottomLeftTable(partitionType, blockSize);
        return ((hasBottomLeftTable[index1] >> index2) & 1) > 0;
    }

    private static Span<byte> GetHasTopRightTable(Av1PartitionType partition, Av1BlockSize blockSize)
    {
        byte[]? ret;

        // If this is a mixed vertical partition, look up block size in vertical order.
        if (partition is Av1PartitionType.VerticalA or Av1PartitionType.VerticalB)
        {
            DebugGuard.MustBeLessThan((int)blockSize, (int)Av1BlockSize.SizeS, nameof(blockSize));
            ret = HasTopRightVerticalTables[(int)blockSize];
        }
        else
        {
            ret = HasTopRightTables[(int)blockSize];
        }

        DebugGuard.NotNull(ret, nameof(ret));
        return ret;
    }

    private static Span<byte> GetHasBottomLeftTable(Av1PartitionType partition, Av1BlockSize blockSize)
    {
        byte[]? ret;

        // If this is a mixed vertical partition, look up block size in vertical order.
        if (partition is Av1PartitionType.VerticalA or Av1PartitionType.VerticalB)
        {
            DebugGuard.MustBeLessThan((int)blockSize, (int)Av1BlockSize.SizeS, nameof(blockSize));
            ret = HasBottomLeftVerticalTables[(int)blockSize];
        }
        else
        {
            ret = HasBottomLeftTables[(int)blockSize];
        }

        DebugGuard.NotNull(ret, nameof(ret));
        return ret;
    }
}
