// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

internal static class Av1TransformSizeExtensions
{
    private static readonly int[] Size2d = [
        16, 64, 256, 1024, 4096, 32, 32, 128, 128, 512, 512, 2048, 2048, 64, 64, 256, 256, 1024, 1024];

    private static readonly Av1TransformSize[] SubTransformSize = [
        Av1TransformSize.Size4x4, // TX_4X4
        Av1TransformSize.Size4x4, // TX_8X8
        Av1TransformSize.Size8x8, // TX_16X16
        Av1TransformSize.Size16x16, // TX_32X32
        Av1TransformSize.Size32x32, // TX_64X64
        Av1TransformSize.Size4x4, // TX_4X8
        Av1TransformSize.Size4x4, // TX_8X4
        Av1TransformSize.Size8x8, // TX_8X16
        Av1TransformSize.Size8x8, // TX_16X8
        Av1TransformSize.Size16x16, // TX_16X32
        Av1TransformSize.Size16x16, // TX_32X16
        Av1TransformSize.Size32x32, // TX_32X64
        Av1TransformSize.Size32x32, // TX_64X32
        Av1TransformSize.Size4x8, // TX_4X16
        Av1TransformSize.Size8x4, // TX_16X4
        Av1TransformSize.Size8x16, // TX_8X32
        Av1TransformSize.Size16x8, // TX_32X8
        Av1TransformSize.Size16x32, // TX_16X64
        Av1TransformSize.Size32x16, // TX_64X16
    ];

    // Transform block width in units.
    private static readonly int[] WideUnit = [1, 2, 4, 8, 16, 1, 2, 2, 4, 4, 8, 8, 16, 1, 4, 2, 8, 4, 16];

    // Transform block height in unit
    private static readonly int[] HighUnit = [1, 2, 4, 8, 16, 2, 1, 4, 2, 8, 4, 16, 8, 4, 1, 8, 2, 16, 4];

    // Transform size conversion into Block Size
    private static readonly Av1BlockSize[] BlockSize = [
        Av1BlockSize.Block4x4, // TX_4X4
        Av1BlockSize.Block8x8, // TX_8X8
        Av1BlockSize.Block16x16, // TX_16X16
        Av1BlockSize.Block32x32, // TX_32X32
        Av1BlockSize.Block64x64, // TX_64X64
        Av1BlockSize.Block4x8, // TX_4X8
        Av1BlockSize.Block8x4, // TX_8X4
        Av1BlockSize.Block8x16, // TX_8X16
        Av1BlockSize.Block16x8, // TX_16X8
        Av1BlockSize.Block16x32, // TX_16X32
        Av1BlockSize.Block32x16, // TX_32X16
        Av1BlockSize.Block32x64, // TX_32X64
        Av1BlockSize.Block64x32, // TX_64X32
        Av1BlockSize.Block4x16, // TX_4X16
        Av1BlockSize.Block16x4, // TX_16X4
        Av1BlockSize.Block8x32, // TX_8X32
        Av1BlockSize.Block32x8, // TX_32X8
        Av1BlockSize.Block16x64, // TX_16X64
        Av1BlockSize.Block64x16, // TX_64X16
    ];

    private static readonly Av1TransformSize[] SquareMap = [
        Av1TransformSize.Size4x4,    // TX_4X4
        Av1TransformSize.Size8x8,    // TX_8X8
        Av1TransformSize.Size16x16,  // TX_16X16
        Av1TransformSize.Size32x32,  // TX_32X32
        Av1TransformSize.Size64x64,  // TX_64X64
        Av1TransformSize.Size4x4,    // TX_4X8
        Av1TransformSize.Size4x4,    // TX_8X4
        Av1TransformSize.Size8x8,    // TX_8X16
        Av1TransformSize.Size8x8,    // TX_16X8
        Av1TransformSize.Size16x16,  // TX_16X32
        Av1TransformSize.Size16x16,  // TX_32X16
        Av1TransformSize.Size32x32,  // TX_32X64
        Av1TransformSize.Size32x32,  // TX_64X32
        Av1TransformSize.Size4x4,    // TX_4X16
        Av1TransformSize.Size4x4,    // TX_16X4
        Av1TransformSize.Size8x8,    // TX_8X32
        Av1TransformSize.Size8x8,    // TX_32X8
        Av1TransformSize.Size16x16,  // TX_16X64
        Av1TransformSize.Size16x16,  // TX_64X16
    ];

    private static readonly Av1TransformSize[] SquareUpMap = [
        Av1TransformSize.Size4x4,    // TX_4X4
        Av1TransformSize.Size8x8,    // TX_8X8
        Av1TransformSize.Size16x16,  // TX_16X16
        Av1TransformSize.Size32x32,  // TX_32X32
        Av1TransformSize.Size64x64,  // TX_64X64
        Av1TransformSize.Size8x8,    // TX_4X8
        Av1TransformSize.Size8x8,    // TX_8X4
        Av1TransformSize.Size16x16,  // TX_8X16
        Av1TransformSize.Size16x16,  // TX_16X8
        Av1TransformSize.Size32x32,  // TX_16X32
        Av1TransformSize.Size32x32,  // TX_32X16
        Av1TransformSize.Size64x64,  // TX_32X64
        Av1TransformSize.Size64x64,  // TX_64X32
        Av1TransformSize.Size16x16,  // TX_4X16
        Av1TransformSize.Size16x16,  // TX_16X4
        Av1TransformSize.Size32x32,  // TX_8X32
        Av1TransformSize.Size32x32,  // TX_32X8
        Av1TransformSize.Size64x64,  // TX_16X64
        Av1TransformSize.Size64x64,  // TX_64X16
    ];

    // This is computed as:
    // min(transform_width_log2, 5) + min(transform_height_log2, 5) - 4.
    private static readonly int[] Log2Minus4 = [
        0, // TX_4X4
        2, // TX_8X8
        4, // TX_16X16
        6, // TX_32X32
        6, // TX_64X64
        1, // TX_4X8
        1, // TX_8X4
        3, // TX_8X16
        3, // TX_16X8
        5, // TX_16X32
        5, // TX_32X16
        6, // TX_32X64
        6, // TX_64X32
        2, // TX_4X16
        2, // TX_16X4
        4, // TX_8X32
        4, // TX_32X8
        5, // TX_16X64
        5, // TX_64X16
    ];

    // Transform block width in log2
    private static readonly int[] BlockWidthLog2 = [
        2, 3, 4, 5, 6, 2, 3, 3, 4, 4, 5, 5, 6, 2, 4, 3, 5, 4, 6,
    ];

    // Transform block height in log2
    private static readonly int[] BlockHeightLog2 = [
        2, 3, 4, 5, 6, 3, 2, 4, 3, 5, 4, 6, 5, 4, 2, 5, 3, 6, 4,
    ];

    public static int GetSize2d(this Av1TransformSize size) => Size2d[(int)size];

    public static int GetScale(this Av1TransformSize size)
    {
        int pels = Size2d[(int)size];
        return (pels > 1024) ? 2 : (pels > 256) ? 1 : 0;
    }

    public static int GetWidth(this Av1TransformSize size) => WideUnit[(int)size] << 2;

    public static int GetHeight(this Av1TransformSize size) => HighUnit[(int)size] << 2;

    public static int Get4x4WideCount(this Av1TransformSize size) => WideUnit[(int)size];

    public static int Get4x4HighCount(this Av1TransformSize size) => HighUnit[(int)size];

    public static Av1TransformSize GetSubSize(this Av1TransformSize size) => SubTransformSize[(int)size];

    public static Av1TransformSize GetSquareSize(this Av1TransformSize size) => SquareMap[(int)size];

    public static Av1TransformSize GetSquareUpSize(this Av1TransformSize size) => SquareUpMap[(int)size];

    public static Av1BlockSize ToBlockSize(this Av1TransformSize transformSize) => BlockSize[(int)transformSize];

    public static int GetLog2Minus4(this Av1TransformSize size) => Log2Minus4[(int)size];

    public static Av1TransformSize GetAdjusted(this Av1TransformSize size) => size switch
    {
        Av1TransformSize.Size64x64 or Av1TransformSize.Size64x32 or Av1TransformSize.Size32x64 => Av1TransformSize.Size32x32,
        Av1TransformSize.Size64x16 => Av1TransformSize.Size32x16,
        Av1TransformSize.Size16x64 => Av1TransformSize.Size16x32,
        _ => size
    };

    public static int GetBlockWidthLog2(this Av1TransformSize size) => BlockWidthLog2[(int)GetAdjusted(size)];

    public static int GetBlockHeightLog2(this Av1TransformSize size) => BlockHeightLog2[(int)GetAdjusted(size)];

    public static int GetRectangleLogRatio(this Av1TransformSize size)
    {
        int col = GetWidth(size);
        int row = GetHeight(size);
        if (col == row)
        {
            return 0;
        }

        if (col > row)
        {
            if (col == row * 2)
            {
                return 1;
            }

            if (col == row * 4)
            {
                return 2;
            }

            throw new InvalidImageContentException("Unsupported transform size");
        }
        else
        {
            if (row == col * 2)
            {
                return -1;
            }

            if (row == col * 4)
            {
                return -2;
            }

            throw new InvalidImageContentException("Unsupported transform size");
        }
    }
}
