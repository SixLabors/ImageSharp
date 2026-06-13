// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1;

internal static class Av1BlockSizeExtensions
{
    private static readonly int[] SizeWide = [1, 1, 2, 2, 2, 4, 4, 4, 8, 8, 8, 16, 16, 16, 32, 32, 1, 4, 2, 8, 4, 16];
    private static readonly int[] SizeHigh = [1, 2, 1, 2, 4, 2, 4, 8, 4, 8, 16, 8, 16, 32, 16, 32, 4, 1, 8, 2, 16, 4];

    // The Subsampled_Size table in the spec (Section 5.11.38. Get plane residual size function).
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

    private static readonly Av1TransformSize[] MaxTransformSize = [
        Av1TransformSize.Size4x4, Av1TransformSize.Size4x8, Av1TransformSize.Size8x4, Av1TransformSize.Size8x8,
        Av1TransformSize.Size8x16, Av1TransformSize.Size16x8, Av1TransformSize.Size16x16, Av1TransformSize.Size16x32,
        Av1TransformSize.Size32x16, Av1TransformSize.Size32x32, Av1TransformSize.Size32x64, Av1TransformSize.Size64x32,
        Av1TransformSize.Size64x64, Av1TransformSize.Size64x64, Av1TransformSize.Size64x64, Av1TransformSize.Size64x64,
        Av1TransformSize.Size4x16, Av1TransformSize.Size16x4, Av1TransformSize.Size8x32, Av1TransformSize.Size32x8,
        Av1TransformSize.Size16x64, Av1TransformSize.Size64x16
    ];

    private static readonly int[] PelsLog2Count =
        [4, 5, 5, 6, 7, 7, 8, 9, 9, 10, 11, 11, 12, 13, 13, 14, 6, 6, 8, 8, 10, 10];

    private static readonly Av1BlockSize[][] HeightWidthToSize = [
        [Av1BlockSize.Block4x4, Av1BlockSize.Block4x8, Av1BlockSize.Block4x16, Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Invalid],
        [Av1BlockSize.Block8x4, Av1BlockSize.Block8x8, Av1BlockSize.Block8x16, Av1BlockSize.Block8x32, Av1BlockSize.Invalid, Av1BlockSize.Invalid],
        [Av1BlockSize.Block16x4, Av1BlockSize.Block16x8, Av1BlockSize.Block16x16, Av1BlockSize.Block16x32, Av1BlockSize.Block16x64, Av1BlockSize.Invalid],
        [Av1BlockSize.Invalid, Av1BlockSize.Block32x8, Av1BlockSize.Block32x16, Av1BlockSize.Block32x32, Av1BlockSize.Block32x64, Av1BlockSize.Invalid],
        [Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block64x16, Av1BlockSize.Block64x32, Av1BlockSize.Block64x64, Av1BlockSize.Block64x128],
        [Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Invalid, Av1BlockSize.Block128x64, Av1BlockSize.Block128x128]
    ];

    public static int Get4x4WideCount(this Av1BlockSize blockSize) => SizeWide[(int)blockSize];

    public static int Get4x4HighCount(this Av1BlockSize blockSize) => SizeHigh[(int)blockSize];

    /// <summary>
    /// Gets the <see cref="Av1BlockSize"/> given by the Log2 of the width and height.
    /// </summary>
    /// <param name="widthLog2">Log2 of the width value.</param>
    /// <param name="heightLog2">Log2 of the height value.</param>
    /// <returns>The <see cref="Av1BlockSize"/>.</returns>
    public static Av1BlockSize FromWidthAndHeight(uint widthLog2, uint heightLog2) => HeightWidthToSize[heightLog2][widthLog2];

    /// <summary>
    /// Returns the width of the block in samples.
    /// </summary>
    public static int GetWidth(this Av1BlockSize blockSize)
        => Get4x4WideCount(blockSize) << 2;

    /// <summary>
    /// Returns of the height of the block in 4 samples.
    /// </summary>
    public static int GetHeight(this Av1BlockSize blockSize)
        => Get4x4HighCount(blockSize) << 2;

    /// <summary>
    /// Returns base 2 logarithm of the width of the block in units of 4 samples.
    /// </summary>
    public static int Get4x4WidthLog2(this Av1BlockSize blockSize)
        => Av1Math.Log2(Get4x4WideCount(blockSize));

    /// <summary>
    /// Returns base 2 logarithm of the height of the block in units of 4 samples.
    /// </summary>
    public static int Get4x4HeightLog2(this Av1BlockSize blockSize)
        => Av1Math.Log2(Get4x4HighCount(blockSize));

    /// <summary>
    /// Returns the block size of a sub sampled block.
    /// </summary>
    public static Av1BlockSize GetSubsampled(this Av1BlockSize blockSize, bool subX, bool subY)
        => GetSubsampled(blockSize, subX ? 1 : 0, subY ? 1 : 0);

    /// <summary>
    /// Returns the block size of a sub sampled block.
    /// </summary>
    public static Av1BlockSize GetSubsampled(this Av1BlockSize blockSize, int subX, int subY)
    {
        if (blockSize == Av1BlockSize.Invalid)
        {
            return Av1BlockSize.Invalid;
        }

        return SubSampled[(int)blockSize][subX][subY];
    }

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
    /// Returns the largest transform size that can be used for blocks of given size.
    /// The can be either a square or rectangular block.
    /// </summary>
    public static Av1TransformSize GetMaximumTransformSize(this Av1BlockSize blockSize)
        => MaxTransformSize[(int)blockSize];

    public static int GetPelsLog2Count(this Av1BlockSize blockSize)
        => PelsLog2Count[(int)blockSize];
}
