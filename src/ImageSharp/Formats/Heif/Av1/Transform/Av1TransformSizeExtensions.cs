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

    public static int GetScale(this Av1TransformSize size)
    {
        int pels = Size2d[(int)size];
        return (pels > 1024) ? 2 : (pels > 256) ? 1 : 0;
    }

    public static int GetWidth(this Av1TransformSize size) => (int)size;

    public static int GetHeight(this Av1TransformSize size) => (int)size;

    public static int GetWidthLog2(this Av1TransformSize size) => (int)size;

    public static int GetHeightLog2(this Av1TransformSize size) => (int)size;

    public static int Get4x4WideCount(this Av1TransformSize size) => WideUnit[(int)size];

    public static int Get4x4HighCount(this Av1TransformSize size) => HighUnit[(int)size];

    public static Av1TransformSize GetSubSize(this Av1TransformSize size) => SubTransformSize[(int)size];
}
