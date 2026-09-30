// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <summary>
/// Produces AV1 wedge masks in caller-owned plane-sized storage.
/// </summary>
/// <remarks>
/// Every row of a wedge master is a contiguous run of a one-dimensional prototype: a constant, a slice, a reversed
/// slice, or two slices interleaved sample by sample. A mask row is therefore built with vector copies and
/// interleaves, and chroma rows are averaged with vector arithmetic. Reference: init_wedge_master_masks() and
/// get_wedge_mask_inplace(), which read the same masters.
/// </remarks>
internal static class Av1WedgeMask
{
    private const int MaximumAlpha = 64;
    private const int MasterSize = 64;

    /// <summary>
    /// The number of clamping samples before and after each padded prototype.
    /// </summary>
    private const int PrototypePadding = 64;

    /// <summary>
    /// Gets the odd-row oblique prototype defined by the reference decoder, padded with its edge values so that
    /// a shifted read needs no clamp.
    /// </summary>
    private static ReadOnlySpan<byte> PaddedObliqueOdd =>
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 2, 6, 18,
        37, 53, 60, 63, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
        64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
        64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
        64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
        64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
        64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
    ];

    /// <summary>
    /// Gets the even-row oblique prototype defined by the reference decoder, padded with its edge values so that
    /// a shifted read needs no clamp.
    /// </summary>
    private static ReadOnlySpan<byte> PaddedObliqueEven =>
    [
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 4, 11, 27,
        46, 58, 62, 63, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
        64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
        64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
        64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
        64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64, 64,
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

        // The sign flip complements the master samples before the chroma average; the caller's inversion
        // complements the averaged mask, since a rounded average of complements is not always the complement of
        // the rounded average.
        bool complement = negative;
        int masterWidth = width << subX;
        Span<byte> upper = stackalloc byte[MasterSize];
        Span<byte> lower = stackalloc byte[MasterSize];
        Span<byte> averaged = stackalloc byte[MasterSize];
        upper.Clear();
        lower.Clear();

        // Chroma masks are the rounded average of the corresponding two or four luma-mask samples. Producing the
        // plane mask once keeps the vector blend contiguous and avoids gathering mask bytes in every SIMD lane.
        for (int row = 0; row < height; row++)
        {
            Span<byte> destinationRow = destination.Slice(row * destinationStride, width);
            int lumaRow = masterRow + (row << subY);
            BuildMasterRow(direction, complement, lumaRow, masterColumn, masterWidth, upper);
            if (subY != 0)
            {
                BuildMasterRow(direction, complement, lumaRow + 1, masterColumn, masterWidth, lower);
            }

            if (subX == 0 && subY == 0)
            {
                upper[..width].CopyTo(destinationRow);
            }
            else
            {
                AverageMasterRows(upper, lower, subX, subY, averaged);
                averaged[..width].CopyTo(destinationRow);
            }

            if (invert)
            {
                TensorPrimitives.Subtract((byte)MaximumAlpha, destinationRow, destinationRow);
            }
        }
    }

    /// <summary>
    /// Writes a run of one wedge master row. Reference: the master construction of init_wedge_master_masks().
    /// </summary>
    /// <param name="direction">The wedge direction.</param>
    /// <param name="complement">Whether to complement the run.</param>
    /// <param name="row">The master row.</param>
    /// <param name="column">The master column of the run's first sample.</param>
    /// <param name="length">The run length, an even number.</param>
    /// <param name="destination">Receives the run.</param>
    private static void BuildMasterRow(int direction, bool complement, int row, int column, int length, Span<byte> destination)
    {
        Span<byte> run = destination[..length];
        switch (direction)
        {
            case 0:
                // WEDGE_VERTICAL: every sample of a row is the prototype value at that row.
                run.Fill(MasterVertical[row]);
                break;
            case 1:
                // WEDGE_HORIZONTAL: the row is the prototype itself.
                MasterVertical.Slice(column, length).CopyTo(run);
                break;
            case 2:
                // WEDGE_OBLIQUE27: the transpose of the oblique-63 master, so even and odd columns read the two
                // prototypes at consecutive positions.
                InterleavePrototypes(row - 16 + (column >> 1), run);
                break;
            case 3:
                // WEDGE_OBLIQUE63: the row's prototype shifted by half the row index.
                ReadOnlySpan<byte> prototype = (row & 1) != 0 ? PaddedObliqueOdd : PaddedObliqueEven;
                int shift = ((row & 1) != 0 ? 15 : 16) - (row >> 1);
                prototype.Slice(PrototypePadding + column - shift, length).CopyTo(run);
                break;
            case 4:
                // WEDGE_OBLIQUE117: the complemented, horizontally mirrored oblique-63 master.
                ReadOnlySpan<byte> mirrored = (row & 1) != 0 ? PaddedObliqueOdd : PaddedObliqueEven;
                int mirroredShift = ((row & 1) != 0 ? 15 : 16) - (row >> 1);
                mirrored.Slice(PrototypePadding + (MasterSize - column - length) - mirroredShift, length).CopyTo(run);
                run.Reverse();
                complement = !complement;
                break;
            default:
                // WEDGE_OBLIQUE153: the complemented, vertically mirrored oblique-27 master.
                InterleavePrototypes(MasterSize - 1 - row - 16 + (column >> 1), run);
                complement = !complement;
                break;
        }

        if (complement)
        {
            TensorPrimitives.Subtract((byte)MaximumAlpha, run, run);
        }
    }

    /// <summary>
    /// Writes the even columns from the even prototype and the odd columns from the odd prototype, both starting
    /// at one prototype position and advancing one position per column pair.
    /// </summary>
    /// <param name="position">The even prototype position of the first column; the odd prototype starts one later.</param>
    /// <param name="run">Receives the interleaved run, of even length.</param>
    private static void InterleavePrototypes(int position, Span<byte> run)
    {
        ref byte even = ref Unsafe.Add(ref MemoryMarshal.GetReference(PaddedObliqueEven), PrototypePadding + position);
        ref byte odd = ref Unsafe.Add(ref MemoryMarshal.GetReference(PaddedObliqueOdd), PrototypePadding + position + 1);
        ref byte destination = ref MemoryMarshal.GetReference(run);

        // A run holds at most 64 samples, so 32 pairs; each 128-bit step interleaves sixteen pairs. The padded
        // prototypes and the 64-byte run buffers keep the final step's reads and writes in bounds.
        for (int pair = 0; pair < run.Length >> 1; pair += Vector128<byte>.Count)
        {
            Vector128<byte> evenSamples = Vector128.LoadUnsafe(ref even, (nuint)pair);
            Vector128<byte> oddSamples = Vector128.LoadUnsafe(ref odd, (nuint)pair);
            Vector128_.UnpackLow(evenSamples, oddSamples).StoreUnsafe(ref destination, (nuint)(2 * pair));
            if ((2 * pair) + Vector128<byte>.Count < MasterSize)
            {
                Vector128_.UnpackHigh(evenSamples, oddSamples).StoreUnsafe(ref destination, (nuint)((2 * pair) + Vector128<byte>.Count));
            }
        }
    }

    /// <summary>
    /// Averages one or two master rows into a subsampled mask row with the rounding of the chroma wedge mask.
    /// Reference: the subsampled masks of av1_init_wedge_masks().
    /// </summary>
    /// <param name="upper">The first master row.</param>
    /// <param name="lower">The second master row, used when the plane is vertically subsampled.</param>
    /// <param name="subX">The horizontal plane subsampling shift.</param>
    /// <param name="subY">The vertical plane subsampling shift.</param>
    /// <param name="destination">Receives the averaged row.</param>
    private static void AverageMasterRows(ReadOnlySpan<byte> upper, ReadOnlySpan<byte> lower, int subX, int subY, Span<byte> destination)
    {
        ref byte upperBase = ref MemoryMarshal.GetReference(upper);
        ref byte lowerBase = ref MemoryMarshal.GetReference(lower);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
        if (subX == 0)
        {
            // Only rows are paired: (a + b + 1) >> 1 is the rounding byte average.
            for (nuint i = 0; i < MasterSize; i += (nuint)Vector128<byte>.Count)
            {
                Vector128_.Average(Vector128.LoadUnsafe(ref upperBase, i), Vector128.LoadUnsafe(ref lowerBase, i)).StoreUnsafe(ref destinationBase, i);
            }

            return;
        }

        // Adjacent column pairs are summed in sixteen-bit lanes; the second row adds its pairs when present.
        int shift = 1 + subY;
        Vector128<short> rounding = Vector128.Create((short)(1 << (shift - 1)));
        Vector128<sbyte> ones = Vector128.Create((sbyte)1);
        for (nuint i = 0; i < MasterSize; i += 2 * (nuint)Vector128<byte>.Count)
        {
            Vector128<short> first = Vector128_.MultiplyAddAdjacent(Vector128.LoadUnsafe(ref upperBase, i), ones);
            Vector128<short> second = Vector128_.MultiplyAddAdjacent(Vector128.LoadUnsafe(ref upperBase, i + 16), ones);
            if (subY != 0)
            {
                first += Vector128_.MultiplyAddAdjacent(Vector128.LoadUnsafe(ref lowerBase, i), ones);
                second += Vector128_.MultiplyAddAdjacent(Vector128.LoadUnsafe(ref lowerBase, i + 16), ones);
            }

            first = (first + rounding) >> shift;
            second = (second + rounding) >> shift;
            Vector128.Narrow(first.AsUInt16(), second.AsUInt16()).StoreUnsafe(ref destinationBase, i >> 1);
        }
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
