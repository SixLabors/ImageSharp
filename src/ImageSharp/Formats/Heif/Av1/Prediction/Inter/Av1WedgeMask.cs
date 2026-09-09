// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <summary>
/// Produces AV1 wedge masks in caller-owned plane-sized storage.
/// </summary>
internal static class Av1WedgeMask
{
    private const int MaximumAlpha = 64;
    private const int MasterSize = 64;

    /// <summary>
    /// Gets the odd-row oblique prototype defined by the reference decoder.
    /// </summary>
    private static ReadOnlySpan<byte> MasterObliqueOdd =>
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 6, 18,
        37, 53, 60, 63, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
        64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
    ];

    /// <summary>
    /// Gets the even-row oblique prototype defined by the reference decoder.
    /// </summary>
    private static ReadOnlySpan<byte> MasterObliqueEven =>
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 4, 11, 27,
        46, 58, 62, 63, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
        64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
    ];

    /// <summary>
    /// Gets the vertical prototype defined by the reference decoder.
    /// </summary>
    private static ReadOnlySpan<byte> MasterVertical =>
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 7, 21,
        43, 57, 62, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
        64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
    ];

    /// <summary>
    /// Gets the codebook used when block height exceeds block width.
    /// </summary>
    private static ReadOnlySpan<byte> HeightGreaterCodebook =>
    [
        2, 4, 4, 3, 4, 4, 4, 4, 4, 5, 4, 4,
        0, 4, 2, 0, 4, 4, 0, 4, 6, 1, 4, 4,
        2, 4, 2, 2, 4, 6, 5, 4, 2, 5, 4, 6,
        3, 2, 4, 3, 6, 4, 4, 2, 4, 4, 6, 4,
    ];

    /// <summary>
    /// Gets the codebook used when block width exceeds block height.
    /// </summary>
    private static ReadOnlySpan<byte> HeightLessCodebook =>
    [
        2, 4, 4, 3, 4, 4, 4, 4, 4, 5, 4, 4,
        1, 2, 4, 1, 4, 4, 1, 6, 4, 0, 4, 4,
        2, 4, 2, 2, 4, 6, 5, 4, 2, 5, 4, 6,
        3, 2, 4, 3, 6, 4, 4, 2, 4, 4, 6, 4,
    ];

    /// <summary>
    /// Gets the codebook used by square blocks.
    /// </summary>
    private static ReadOnlySpan<byte> EqualCodebook =>
    [
        2, 4, 4, 3, 4, 4, 4, 4, 4, 5, 4, 4,
        0, 4, 2, 0, 4, 6, 1, 2, 4, 1, 6, 4,
        2, 4, 2, 2, 4, 6, 5, 4, 2, 5, 4, 6,
        3, 2, 4, 3, 6, 4, 4, 2, 4, 4, 6, 4,
    ];

    /// <summary>
    /// Fills one luma or subsampled chroma mask for a selected wedge.
    /// </summary>
    /// <param name="destination">The caller-owned plane mask.</param>
    /// <param name="destinationStride">The distance between destination rows.</param>
    /// <param name="blockSize">The luma block size selecting the wedge codebook.</param>
    /// <param name="wedgeIndex">The wedge index in the inclusive range zero through fifteen.</param>
    /// <param name="wedgeSign">The signaled compound wedge orientation.</param>
    /// <param name="subX">The horizontal plane subsampling shift.</param>
    /// <param name="subY">The vertical plane subsampling shift.</param>
    /// <param name="invert">Whether to complement the resulting mask.</param>
    public static void Fill(
        Span<byte> destination,
        int destinationStride,
        Av1BlockSize blockSize,
        int wedgeIndex,
        bool wedgeSign,
        int subX,
        int subY,
        bool invert)
    {
        int lumaWidth = blockSize.GetWidth();
        int lumaHeight = blockSize.GetHeight();
        int width = Math.Max(4, lumaWidth >> subX);
        int height = Math.Max(4, lumaHeight >> subY);
        ReadOnlySpan<byte> codebook = lumaHeight > lumaWidth
            ? HeightGreaterCodebook
            : lumaHeight < lumaWidth ? HeightLessCodebook : EqualCodebook;

        int codebookOffset = wedgeIndex * 3;
        int direction = codebook[codebookOffset];
        int horizontalOffset = (codebook[codebookOffset + 1] * lumaWidth) >> 3;
        int verticalOffset = (codebook[codebookOffset + 2] * lumaHeight) >> 3;
        bool negative = wedgeSign ^ GetSignFlip(blockSize, wedgeIndex);
        int masterRow = (MasterSize / 2) - verticalOffset;
        int masterColumn = (MasterSize / 2) - horizontalOffset;

        // Chroma masks are the rounded average of the corresponding two or four luma-mask samples. Producing the
        // plane mask once keeps the vector blend contiguous and avoids gathering mask bytes in every SIMD lane.
        for (int row = 0; row < height; row++)
        {
            Span<byte> destinationRow = destination.Slice(row * destinationStride, width);
            int lumaRow = row << subY;
            for (int column = 0; column < width; column++)
            {
                int lumaColumn = column << subX;
                int mask = GetMasterValue(direction, negative, masterRow + lumaRow, masterColumn + lumaColumn);
                if (subX != 0)
                {
                    mask += GetMasterValue(direction, negative, masterRow + lumaRow, masterColumn + lumaColumn + 1);
                }

                if (subY != 0)
                {
                    int lowerMask = GetMasterValue(direction, negative, masterRow + lumaRow + 1, masterColumn + lumaColumn);
                    if (subX != 0)
                    {
                        lowerMask += GetMasterValue(direction, negative, masterRow + lumaRow + 1, masterColumn + lumaColumn + 1);
                    }

                    mask += lowerMask;
                }

                int sampleCountShift = subX + subY;
                if (sampleCountShift != 0)
                {
                    mask = (mask + (1 << (sampleCountShift - 1))) >> sampleCountShift;
                }

                destinationRow[column] = (byte)(invert ? MaximumAlpha - mask : mask);
            }
        }
    }

    /// <summary>
    /// Reads one value from the generated 64 by 64 master mask.
    /// </summary>
    private static int GetMasterValue(int direction, bool negative, int row, int column)
    {
        int value = direction switch
        {
            0 => MasterVertical[row],
            1 => MasterVertical[column],
            2 => GetOblique63(column, row),
            3 => GetOblique63(row, column),
            4 => MaximumAlpha - GetOblique63(row, MasterSize - 1 - column),
            _ => MaximumAlpha - GetOblique63(column, MasterSize - 1 - row),
        };

        return negative ? MaximumAlpha - value : value;
    }

    /// <summary>
    /// Reads one value from the shifted oblique-63 master prototype.
    /// </summary>
    private static int GetOblique63(int row, int column)
    {
        bool oddRow = (row & 1) != 0;
        int shift = (oddRow ? 15 : 16) - (row >> 1);
        int sourceColumn = Av1Math.Clip3(0, MasterSize - 1, column - shift);
        return oddRow ? MasterObliqueOdd[sourceColumn] : MasterObliqueEven[sourceColumn];
    }

    /// <summary>
    /// Gets the reference decoder's canonical sign flip for a block and wedge index.
    /// </summary>
    private static bool GetSignFlip(Av1BlockSize blockSize, int wedgeIndex)
    {
        ReadOnlySpan<byte> signFlips = blockSize switch
        {
            Av1BlockSize.Block8x8 or Av1BlockSize.Block16x16 or Av1BlockSize.Block32x32 =>
                [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 1, 0, 1],
            Av1BlockSize.Block8x32 =>
                [1, 1, 1, 1, 0, 1, 1, 1, 0, 1, 0, 1, 1, 1, 0, 1],
            Av1BlockSize.Block32x8 =>
                [1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 0, 1, 0, 1],
            _ =>
                [1, 1, 1, 1, 0, 1, 1, 1, 1, 1, 0, 1, 1, 1, 0, 1],
        };

        return signFlips[wedgeIndex] != 0;
    }
}
