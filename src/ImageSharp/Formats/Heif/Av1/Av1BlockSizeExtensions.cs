// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

/// <summary>
/// Provides dimensions, chroma subsampling, and transform limits for AV1 block sizes.
/// </summary>
internal static class Av1BlockSizeExtensions
{
    /// <summary>
    /// The width of each block size in units of four samples.
    /// </summary>
    private static readonly int[] SizeWide = [1, 1, 2, 2, 2, 4, 4, 4, 8, 8, 8, 16, 16, 16, 32, 32, 1, 4, 2, 8, 4, 16];

    /// <summary>
    /// The height of each block size in units of four samples.
    /// </summary>
    private static readonly int[] SizeHigh = [1, 2, 1, 2, 4, 2, 4, 8, 4, 8, 16, 8, 16, 32, 16, 32, 4, 1, 8, 2, 16, 4];

    /// <summary>
    /// Maps each luma block size and pair of chroma subsampling shifts to its residual-plane block size.
    /// </summary>
    private static readonly Av1BlockSize[][][] SubSampled =
        [

            // ss_x == 0    ss_x == 0        ss_x == 1      ss_x == 1
            // ss_y == 0    ss_y == 1        ss_y == 0      ss_y == 1
            [[Av1BlockSize.Block4x4, Av1BlockSize.Block4x4], [Av1BlockSize.Block4x4, Av1BlockSize.Block4x4]],
            [[Av1BlockSize.Block4x8, Av1BlockSize.Block4x4], [Av1BlockSize.Invalid, Av1BlockSize.Block4x4]],
            [[Av1BlockSize.Block8x4, Av1BlockSize.Invalid], [Av1BlockSize.Block4x4, Av1BlockSize.Block4x4]],
            [[Av1BlockSize.Block8x8, Av1BlockSize.Block8x4], [Av1BlockSize.Block4x8, Av1BlockSize.Block4x4]],
            [[Av1BlockSize.Block8x16, Av1BlockSize.Block8x8], [Av1BlockSize.Invalid, Av1BlockSize.Block4x8]],
            [[Av1BlockSize.Block16x8, Av1BlockSize.Invalid], [Av1BlockSize.Block8x8, Av1BlockSize.Block8x4]],
            [[Av1BlockSize.Block16x16, Av1BlockSize.Block16x8], [Av1BlockSize.Block8x16, Av1BlockSize.Block8x8]],
            [[Av1BlockSize.Block16x32, Av1BlockSize.Block16x16], [Av1BlockSize.Invalid, Av1BlockSize.Block8x16]],
            [[Av1BlockSize.Block32x16, Av1BlockSize.Invalid], [Av1BlockSize.Block16x16, Av1BlockSize.Block16x8]],
            [[Av1BlockSize.Block32x32, Av1BlockSize.Block32x16], [Av1BlockSize.Block16x32, Av1BlockSize.Block16x16]],
            [[Av1BlockSize.Block32x64, Av1BlockSize.Block32x32], [Av1BlockSize.Invalid, Av1BlockSize.Block16x32]],
            [[Av1BlockSize.Block64x32, Av1BlockSize.Invalid], [Av1BlockSize.Block32x32, Av1BlockSize.Block32x16]],
            [[Av1BlockSize.Block64x64, Av1BlockSize.Block64x32], [Av1BlockSize.Block32x64, Av1BlockSize.Block32x32]],
            [[Av1BlockSize.Block64x128, Av1BlockSize.Block64x64], [Av1BlockSize.Invalid, Av1BlockSize.Block32x64]],
            [[Av1BlockSize.Block128x64, Av1BlockSize.Invalid], [Av1BlockSize.Block64x64, Av1BlockSize.Block64x32]],
            [[Av1BlockSize.Block128x128, Av1BlockSize.Block128x64], [Av1BlockSize.Block64x128, Av1BlockSize.Block64x64]],
            [[Av1BlockSize.Block4x16, Av1BlockSize.Block4x8], [Av1BlockSize.Invalid, Av1BlockSize.Block4x8]],
            [[Av1BlockSize.Block16x4, Av1BlockSize.Invalid], [Av1BlockSize.Block8x4, Av1BlockSize.Block8x4]],
            [[Av1BlockSize.Block8x32, Av1BlockSize.Block8x16], [Av1BlockSize.Invalid, Av1BlockSize.Block4x16]],
            [[Av1BlockSize.Block32x8, Av1BlockSize.Invalid], [Av1BlockSize.Block16x8, Av1BlockSize.Block16x4]],
            [[Av1BlockSize.Block16x64, Av1BlockSize.Block16x32], [Av1BlockSize.Invalid, Av1BlockSize.Block8x32]],
            [[Av1BlockSize.Block64x16, Av1BlockSize.Invalid], [Av1BlockSize.Block32x16, Av1BlockSize.Block32x8]]
        ];

    /// <summary>
    /// Maps each block size to its largest permitted transform size.
    /// </summary>
    private static readonly Av1TransformSize[] MaxTransformSize = [
        Av1TransformSize.Size4x4, Av1TransformSize.Size4x8, Av1TransformSize.Size8x4, Av1TransformSize.Size8x8,
        Av1TransformSize.Size8x16, Av1TransformSize.Size16x8, Av1TransformSize.Size16x16, Av1TransformSize.Size16x32,
        Av1TransformSize.Size32x16, Av1TransformSize.Size32x32, Av1TransformSize.Size32x64, Av1TransformSize.Size64x32,
        Av1TransformSize.Size64x64, Av1TransformSize.Size64x64, Av1TransformSize.Size64x64, Av1TransformSize.Size64x64,
        Av1TransformSize.Size4x16, Av1TransformSize.Size16x4, Av1TransformSize.Size8x32, Av1TransformSize.Size32x8,
        Av1TransformSize.Size16x64, Av1TransformSize.Size64x16
    ];

    /// <summary>
    /// Contains the base-two logarithm of the sample count for each block size.
    /// </summary>
    private static readonly int[] PelsLog2Count =
        [4, 5, 5, 6, 7, 7, 8, 9, 9, 10, 11, 11, 12, 13, 13, 14, 6, 6, 8, 8, 10, 10];

    /// <summary>
    /// Maps geometry dimension logarithms to an AV1 block size using the mode-decision scan's transposed axis convention.
    /// </summary>
    private static readonly Av1BlockSize[][] HeightWidthToSize = [
        [Av1BlockSize.Block4x4, Av1BlockSize.Block4x8, Av1BlockSize.Block4x16, Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Invalid],
        [Av1BlockSize.Block8x4, Av1BlockSize.Block8x8, Av1BlockSize.Block8x16, Av1BlockSize.Block8x32, Av1BlockSize.Invalid, Av1BlockSize.Invalid],
        [Av1BlockSize.Block16x4, Av1BlockSize.Block16x8, Av1BlockSize.Block16x16, Av1BlockSize.Block16x32, Av1BlockSize.Block16x64, Av1BlockSize.Invalid],
        [Av1BlockSize.Invalid, Av1BlockSize.Block32x8, Av1BlockSize.Block32x16, Av1BlockSize.Block32x32, Av1BlockSize.Block32x64, Av1BlockSize.Invalid],
        [Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block64x16, Av1BlockSize.Block64x32, Av1BlockSize.Block64x64, Av1BlockSize.Block64x128],
        [Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block128x64, Av1BlockSize.Block128x128]
    ];

    /// <summary>
    /// Gets the block width in units of four samples.
    /// </summary>
    /// <param name="blockSize">The block size.</param>
    /// <returns>The number of four-sample columns.</returns>
    public static int Get4x4WideCount(this Av1BlockSize blockSize) => SizeWide[(int)blockSize];

    /// <summary>
    /// Gets the block height in units of four samples.
    /// </summary>
    /// <param name="blockSize">The block size.</param>
    /// <returns>The number of four-sample rows.</returns>
    public static int Get4x4HighCount(this Av1BlockSize blockSize) => SizeHigh[(int)blockSize];

    /// <summary>
    /// Gets the block size from mode-decision geometry dimension logarithms, where zero represents four samples.
    /// </summary>
    /// <param name="widthLog2">The base-two width logarithm minus two.</param>
    /// <param name="heightLog2">The base-two height logarithm minus two.</param>
    /// <returns>The matching block size, or <see cref="Av1BlockSize.Invalid"/> for unsupported dimensions.</returns>
    public static Av1BlockSize FromWidthAndHeight(uint widthLog2, uint heightLog2)
    {
        // Mode-decision geometry is ported with its source axis order, so its size lookup is indexed height first.
        return HeightWidthToSize[heightLog2][widthLog2];
    }

    /// <summary>
    /// Gets the block width in samples.
    /// </summary>
    /// <param name="blockSize">The block size.</param>
    /// <returns>The block width in samples.</returns>
    public static int GetWidth(this Av1BlockSize blockSize)
        => Get4x4WideCount(blockSize) << 2;

    /// <summary>
    /// Gets the block height in samples.
    /// </summary>
    /// <param name="blockSize">The block size.</param>
    /// <returns>The block height in samples.</returns>
    public static int GetHeight(this Av1BlockSize blockSize)
        => Get4x4HighCount(blockSize) << 2;

    /// <summary>
    /// Gets the base-two logarithm of the block width in units of four samples.
    /// </summary>
    /// <param name="blockSize">The block size.</param>
    /// <returns>The base-two logarithm of the four-sample column count.</returns>
    public static int Get4x4WidthLog2(this Av1BlockSize blockSize)
        => Av1Math.Log2(Get4x4WideCount(blockSize));

    /// <summary>
    /// Gets the base-two logarithm of the block height in units of four samples.
    /// </summary>
    /// <param name="blockSize">The block size.</param>
    /// <returns>The base-two logarithm of the four-sample row count.</returns>
    public static int Get4x4HeightLog2(this Av1BlockSize blockSize)
        => Av1Math.Log2(Get4x4HighCount(blockSize));

    /// <summary>
    /// Gets the entropy context group associated with the block size.
    /// </summary>
    /// <param name="blockSize">The block size.</param>
    /// <returns>The zero-based size group in the inclusive range zero through three.</returns>
    public static int GetSizeGroup(this Av1BlockSize blockSize)
    {
        // AV1 section 9.3 groups a block by its smaller dimension in 4x4 units and caps that logarithm at three.
        // Deriving the value from the existing geometry tables exactly matches the reference decoder's size_group_lookup table.
        return Math.Min(3, Math.Min(blockSize.Get4x4WidthLog2(), blockSize.Get4x4HeightLog2()));
    }

    /// <summary>
    /// Gets the residual-plane block size for Boolean chroma subsampling flags.
    /// </summary>
    /// <param name="blockSize">The luma block size.</param>
    /// <param name="subX">Indicates horizontal chroma subsampling.</param>
    /// <param name="subY">Indicates vertical chroma subsampling.</param>
    /// <returns>The corresponding residual-plane block size.</returns>
    public static Av1BlockSize GetSubsampled(this Av1BlockSize blockSize, bool subX, bool subY)
        => GetSubsampled(blockSize, subX ? 1 : 0, subY ? 1 : 0);

    /// <summary>
    /// Gets the residual-plane block size for chroma subsampling shifts.
    /// </summary>
    /// <param name="blockSize">The luma block size.</param>
    /// <param name="subX">The horizontal chroma subsampling shift.</param>
    /// <param name="subY">The vertical chroma subsampling shift.</param>
    /// <returns>The corresponding residual-plane block size, or <see cref="Av1BlockSize.Invalid"/> when unavailable.</returns>
    public static Av1BlockSize GetSubsampled(this Av1BlockSize blockSize, int subX, int subY)
    {
        if (blockSize == Av1BlockSize.Invalid)
        {
            return Av1BlockSize.Invalid;
        }

        return SubSampled[(int)blockSize][subX][subY];
    }

    /// <summary>
    /// Determines whether a luma block permits chroma-from-luma prediction.
    /// </summary>
    /// <param name="blockSize">The luma block size.</param>
    /// <param name="isLossless">Indicates whether the block belongs to a lossless segment.</param>
    /// <param name="subX">Indicates horizontal chroma subsampling.</param>
    /// <param name="subY">Indicates vertical chroma subsampling.</param>
    /// <returns><see langword="true"/> when chroma-from-luma prediction is permitted; otherwise, <see langword="false"/>.</returns>
    public static bool AllowsChromaFromLuma(
        this Av1BlockSize blockSize,
        bool isLossless,
        bool subX,
        bool subY)
    {
        if (isLossless)
        {
            // Lossless coding fixes the transform to 4x4, so the subsampled chroma block must have the same dimensions.
            return blockSize.GetSubsampled(subX, subY) == Av1BlockSize.Block4x4;
        }

        return blockSize.GetWidth() <= 32 && blockSize.GetHeight() <= 32;
    }

    /// <summary>
    /// Gets the maximum chroma transform size after applying plane subsampling and AV1 chroma transform limits.
    /// </summary>
    /// <param name="blockSize">The luma block size.</param>
    /// <param name="subX">Indicates horizontal chroma subsampling.</param>
    /// <param name="subY">Indicates vertical chroma subsampling.</param>
    /// <returns>The maximum chroma transform size, or <see cref="Av1TransformSize.Invalid"/> when the plane block size is invalid.</returns>
    public static Av1TransformSize GetMaxUvTransformSize(this Av1BlockSize blockSize, bool subX, bool subY)
    {
        Av1BlockSize planeBlockSize = blockSize.GetSubsampled(subX, subY);
        Av1TransformSize uvTransformSize = Av1TransformSize.Invalid;
        if (planeBlockSize < Av1BlockSize.AllSizes)
        {
            uvTransformSize = planeBlockSize.GetMaximumTransformSize();
        }

        return uvTransformSize switch
        {
            Av1TransformSize.Size64x64 or Av1TransformSize.Size64x32 or Av1TransformSize.Size32x64 => Av1TransformSize.Size32x32,
            Av1TransformSize.Size64x16 => Av1TransformSize.Size32x16,
            Av1TransformSize.Size16x64 => Av1TransformSize.Size16x32,
            _ => uvTransformSize,
        };
    }

    /// <summary>
    /// Gets the largest square or rectangular transform size permitted for a block.
    /// </summary>
    /// <param name="blockSize">The block size.</param>
    /// <returns>The maximum transform size.</returns>
    public static Av1TransformSize GetMaximumTransformSize(this Av1BlockSize blockSize)
        => MaxTransformSize[(int)blockSize];

    /// <summary>
    /// Gets the base-two logarithm of the block's sample count.
    /// </summary>
    /// <param name="blockSize">The block size.</param>
    /// <returns>The base-two logarithm of width multiplied by height.</returns>
    public static int GetPelsLog2Count(this Av1BlockSize blockSize)
        => PelsLog2Count[(int)blockSize];
}
