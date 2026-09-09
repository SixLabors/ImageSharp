// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Provides dimensions, block mappings, scaling values, and subdivision rules for AV1 transform sizes.
/// </summary>
internal static class Av1TransformSizeExtensions
{
    /// <summary>
    /// The coefficient count for each transform-size enum value.
    /// </summary>
    private static readonly int[] Size2d = [
        16, 64, 256, 1024, 4096, 32, 32, 128, 128, 512, 512, 2048, 2048, 64, 64, 256, 256, 1024, 1024];

    /// <summary>
    /// The transform size produced by one level of subdivision for each transform-size enum value.
    /// </summary>
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

    /// <summary>
    /// Transform widths in units of four samples.
    /// </summary>
    private static readonly int[] WideUnit = [1, 2, 4, 8, 16, 1, 2, 2, 4, 4, 8, 8, 16, 1, 4, 2, 8, 4, 16];

    /// <summary>
    /// Transform heights in units of four samples.
    /// </summary>
    private static readonly int[] HighUnit = [1, 2, 4, 8, 16, 2, 1, 4, 2, 8, 4, 16, 8, 4, 1, 8, 2, 16, 4];

    /// <summary>
    /// Maps each transform size to the block size with matching dimensions.
    /// </summary>
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

    /// <summary>
    /// Maps each transform size to the square transform based on its smaller dimension.
    /// </summary>
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

    /// <summary>
    /// Maps each transform size to the square transform based on its larger dimension.
    /// </summary>
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

    /// <summary>
    /// Contains <c>min(log2(width), 5) + min(log2(height), 5) - 4</c> for each transform size.
    /// </summary>
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

    /// <summary>
    /// Transform widths expressed as base-two logarithms of sample counts.
    /// </summary>
    private static readonly int[] BlockWidthLog2 = [
        2, 3, 4, 5, 6, 2, 3, 3, 4, 4, 5, 5, 6, 2, 4, 3, 5, 4, 6,
    ];

    /// <summary>
    /// Transform heights expressed as base-two logarithms of sample counts.
    /// </summary>
    private static readonly int[] BlockHeightLog2 = [
        2, 3, 4, 5, 6, 3, 2, 4, 3, 5, 4, 6, 5, 4, 2, 5, 3, 6, 4,
    ];

    /// <summary>
    /// Gets the number of coefficient positions in a transform block.
    /// </summary>
    /// <param name="size">The transform size.</param>
    /// <returns>The transform width multiplied by its height.</returns>
    public static int GetSize2d(this Av1TransformSize size) => Size2d[(int)size];

    /// <summary>
    /// Gets the inverse-quantization scale category for a transform size.
    /// </summary>
    /// <param name="size">The transform size.</param>
    /// <returns>Zero for up to 256 coefficients, one for up to 1024, or two for larger transforms.</returns>
    public static int GetScale(this Av1TransformSize size)
    {
        int pels = Size2d[(int)size];
        return (pels > 1024) ? 2 : (pels > 256) ? 1 : 0;
    }

    /// <summary>
    /// Gets the transform width in samples.
    /// </summary>
    /// <param name="size">The transform size.</param>
    /// <returns>The transform width in samples.</returns>
    public static int GetWidth(this Av1TransformSize size) => WideUnit[(int)size] << 2;

    /// <summary>
    /// Gets the transform height in samples.
    /// </summary>
    /// <param name="size">The transform size.</param>
    /// <returns>The transform height in samples.</returns>
    public static int GetHeight(this Av1TransformSize size) => HighUnit[(int)size] << 2;

    /// <summary>
    /// Gets the transform width in units of four samples.
    /// </summary>
    /// <param name="size">The transform size.</param>
    /// <returns>The number of four-sample columns.</returns>
    public static int Get4x4WideCount(this Av1TransformSize size) => WideUnit[(int)size];

    /// <summary>
    /// Gets the transform height in units of four samples.
    /// </summary>
    /// <param name="size">The transform size.</param>
    /// <returns>The number of four-sample rows.</returns>
    public static int Get4x4HighCount(this Av1TransformSize size) => HighUnit[(int)size];

    /// <summary>
    /// Gets the next smaller transform size used when a transform block is subdivided.
    /// </summary>
    /// <param name="size">The transform size.</param>
    /// <returns>The transform's subdivision size.</returns>
    public static Av1TransformSize GetSubSize(this Av1TransformSize size) => SubTransformSize[(int)size];

    /// <summary>
    /// Gets the square transform based on the smaller dimension of a rectangular transform.
    /// </summary>
    /// <param name="size">The transform size.</param>
    /// <returns>The corresponding square transform size.</returns>
    public static Av1TransformSize GetSquareSize(this Av1TransformSize size) => SquareMap[(int)size];

    /// <summary>
    /// Gets the square transform based on the larger dimension of a rectangular transform.
    /// </summary>
    /// <param name="size">The transform size.</param>
    /// <returns>The corresponding enclosing square transform size.</returns>
    public static Av1TransformSize GetSquareUpSize(this Av1TransformSize size) => SquareUpMap[(int)size];

    /// <summary>
    /// Gets the block size having the same dimensions as a transform size.
    /// </summary>
    /// <param name="transformSize">The transform size.</param>
    /// <returns>The dimensionally equivalent block size.</returns>
    public static Av1BlockSize ToBlockSize(this Av1TransformSize transformSize) => BlockSize[(int)transformSize];

    /// <summary>
    /// Gets the capped sum of the transform-dimension logarithms minus four.
    /// </summary>
    /// <param name="size">The transform size.</param>
    /// <returns>The context value used by AV1 transform syntax.</returns>
    public static int GetLog2Minus4(this Av1TransformSize size) => Log2Minus4[(int)size];

    /// <summary>
    /// Gets the transform size used by coefficient and quantization tables that cap dimensions at 32 samples.
    /// </summary>
    /// <param name="size">The signaled transform size.</param>
    /// <returns>The adjusted transform size.</returns>
    public static Av1TransformSize GetAdjusted(this Av1TransformSize size) => size switch
    {
        Av1TransformSize.Size64x64 or Av1TransformSize.Size64x32 or Av1TransformSize.Size32x64 => Av1TransformSize.Size32x32,
        Av1TransformSize.Size64x16 => Av1TransformSize.Size32x16,
        Av1TransformSize.Size16x64 => Av1TransformSize.Size16x32,
        _ => size
    };

    /// <summary>
    /// Gets the base-two logarithm of the transform width in samples.
    /// </summary>
    /// <param name="size">The transform size.</param>
    /// <returns>The base-two width logarithm.</returns>
    public static int GetBlockWidthLog2(this Av1TransformSize size) => BlockWidthLog2[(int)size];

    /// <summary>
    /// Gets the base-two logarithm of the transform height in samples.
    /// </summary>
    /// <param name="size">The transform size.</param>
    /// <returns>The base-two height logarithm.</returns>
    public static int GetBlockHeightLog2(this Av1TransformSize size) => BlockHeightLog2[(int)size];

    /// <summary>
    /// Gets the signed base-two ratio between transform width and height.
    /// </summary>
    /// <param name="size">The transform size.</param>
    /// <returns>Zero for square transforms, positive when wider, or negative when taller.</returns>
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
