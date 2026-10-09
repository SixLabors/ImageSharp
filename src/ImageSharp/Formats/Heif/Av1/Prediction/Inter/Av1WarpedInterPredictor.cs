// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1TranslationalInterPredictor;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Reconstructs local and global affine warped-motion prediction blocks.
/// </content>
internal static partial class Av1WarpedInterPredictor
{
    /// <summary>
    /// The number of rows in the horizontal intermediate tile of one warped filter: 8 output rows plus 7 rows of vertical tap support.
    /// </summary>
    private const int WarpedIntermediateRows = 15;

    /// <summary>
    /// The number of source samples in the clamped horizontal window of one tile.
    /// </summary>
    /// <remarks>
    /// The eight-tap windows of a tile contribute samples <c>integerX - 7</c> through <c>integerX + 7</c>.
    /// The vector loads read sixteen samples from the start of each window, so the last window of a tile reads up to <c>integerX + 15</c>.
    /// The clamped copy covers every sample that is read. Only the first fifteen change the result.
    /// </remarks>
    private const int WarpedWindowLength = 24;

    /// <summary>
    /// The number of columns in one warped filter tile.
    /// </summary>
    private const int WarpedTileSize = 8;

    /// <summary>
    /// The number of low model bits removed when addressing the warped filter table.
    /// </summary>
    private const int WarpedDifferencePrecisionBits = 10;

    /// <summary>
    /// The number of fractional positions in one warped pixel.
    /// </summary>
    private const int WarpedPixelPrecisionShifts = 64;

    /// <summary>
    /// The number of signed 16-bit elements required by warped prediction.
    /// </summary>
    public const int WarpedScratchLength = WarpedIntermediateRows * WarpedTileSize;

    /// <summary>
    /// Reconstructs an 8-bit affine warped prediction with the widest supported convolution operator.
    /// </summary>
    /// <param name="source">The reference plane storage.</param>
    /// <param name="sourceStride">The distance between reference rows in samples.</param>
    /// <param name="sourceOrigin">The position of the top-left visible reference sample within <paramref name="source"/>.</param>
    /// <param name="sourceWidth">The visible reference width. Taps outside it read the nearest edge column.</param>
    /// <param name="sourceHeight">The visible reference height. Taps outside it read the nearest edge row.</param>
    /// <param name="destination">The prediction block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="destinationPosition">The plane position of the top-left block sample.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
    /// <param name="subsamplingX">The horizontal subsampling shift of the plane.</param>
    /// <param name="subsamplingY">The vertical subsampling shift of the plane.</param>
    /// <param name="parameters">The affine warp model and its shear parameters.</param>
    /// <param name="scratch">Signed intermediate storage of at least <see cref="WarpedScratchLength"/> elements.</param>
    public static void PredictWarped(
        ReadOnlySpan<byte> source,
        int sourceStride,
        Point sourceOrigin,
        int sourceWidth,
        int sourceHeight,
        Span<byte> destination,
        int destinationStride,
        Point destinationPosition,
        int width,
        int height,
        int subsamplingX,
        int subsamplingY,
        Av1GlobalMotionParameters parameters,
        Span<short> scratch)
        => PredictWarped<WarpedOperator>(
            source,
            sourceStride,
            sourceOrigin,
            sourceWidth,
            sourceHeight,
            destination,
            destinationStride,
            destinationPosition,
            width,
            height,
            subsamplingX,
            subsamplingY,
            parameters,
            scratch,
            useHardwareIntrinsics: true);

    /// <summary>
    /// Reconstructs an 8-bit affine warped reference into the unsigned AV1 compound intermediate format.
    /// </summary>
    /// <param name="source">The reference plane storage.</param>
    /// <param name="sourceStride">The distance between reference rows in samples.</param>
    /// <param name="sourceOrigin">The position of the top-left visible reference sample within <paramref name="source"/>.</param>
    /// <param name="sourceWidth">The visible reference width. Taps outside it read the nearest edge column.</param>
    /// <param name="sourceHeight">The visible reference height. Taps outside it read the nearest edge row.</param>
    /// <param name="destination">The compound intermediate destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="destinationPosition">The plane position of the top-left block sample.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
    /// <param name="subsamplingX">The horizontal subsampling shift of the plane.</param>
    /// <param name="subsamplingY">The vertical subsampling shift of the plane.</param>
    /// <param name="parameters">The affine warp model and its shear parameters.</param>
    /// <param name="scratch">Signed intermediate storage of at least <see cref="WarpedScratchLength"/> elements.</param>
    public static void PredictWarpedCompound(
        ReadOnlySpan<byte> source,
        int sourceStride,
        Point sourceOrigin,
        int sourceWidth,
        int sourceHeight,
        Span<ushort> destination,
        int destinationStride,
        Point destinationPosition,
        int width,
        int height,
        int subsamplingX,
        int subsamplingY,
        Av1GlobalMotionParameters parameters,
        Span<short> scratch)
        => PredictWarpedCompound<WarpedOperator>(
            source,
            sourceStride,
            sourceOrigin,
            sourceWidth,
            sourceHeight,
            destination,
            destinationStride,
            destinationPosition,
            width,
            height,
            subsamplingX,
            subsamplingY,
            parameters,
            scratch,
            useHardwareIntrinsics: true);

    /// <summary>
    /// Reconstructs a high-bit-depth affine warped prediction with the widest supported convolution operator.
    /// </summary>
    /// <param name="source">The reference plane storage.</param>
    /// <param name="sourceStride">The distance between reference rows in samples.</param>
    /// <param name="sourceOrigin">The position of the top-left visible reference sample within <paramref name="source"/>.</param>
    /// <param name="sourceWidth">The visible reference width. Taps outside it read the nearest edge column.</param>
    /// <param name="sourceHeight">The visible reference height. Taps outside it read the nearest edge row.</param>
    /// <param name="destination">The prediction block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="destinationPosition">The plane position of the top-left block sample.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
    /// <param name="subsamplingX">The horizontal subsampling shift of the plane.</param>
    /// <param name="subsamplingY">The vertical subsampling shift of the plane.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="parameters">The affine warp model and its shear parameters.</param>
    /// <param name="scratch">Signed intermediate storage of at least <see cref="WarpedScratchLength"/> elements.</param>
    public static void PredictWarped(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        Point sourceOrigin,
        int sourceWidth,
        int sourceHeight,
        Span<ushort> destination,
        int destinationStride,
        Point destinationPosition,
        int width,
        int height,
        int subsamplingX,
        int subsamplingY,
        int bitDepth,
        Av1GlobalMotionParameters parameters,
        Span<short> scratch)
        => PredictWarped<WarpedOperator>(
            source,
            sourceStride,
            sourceOrigin,
            sourceWidth,
            sourceHeight,
            destination,
            destinationStride,
            destinationPosition,
            width,
            height,
            subsamplingX,
            subsamplingY,
            bitDepth,
            parameters,
            scratch,
            useHardwareIntrinsics: true);

    /// <summary>
    /// Reconstructs a high-bit-depth affine warped reference into the unsigned AV1 compound intermediate format.
    /// </summary>
    /// <param name="source">The reference plane storage.</param>
    /// <param name="sourceStride">The distance between reference rows in samples.</param>
    /// <param name="sourceOrigin">The position of the top-left visible reference sample within <paramref name="source"/>.</param>
    /// <param name="sourceWidth">The visible reference width. Taps outside it read the nearest edge column.</param>
    /// <param name="sourceHeight">The visible reference height. Taps outside it read the nearest edge row.</param>
    /// <param name="destination">The compound intermediate destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="destinationPosition">The plane position of the top-left block sample.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
    /// <param name="subsamplingX">The horizontal subsampling shift of the plane.</param>
    /// <param name="subsamplingY">The vertical subsampling shift of the plane.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="parameters">The affine warp model and its shear parameters.</param>
    /// <param name="scratch">Signed intermediate storage of at least <see cref="WarpedScratchLength"/> elements.</param>
    public static void PredictWarpedCompound(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        Point sourceOrigin,
        int sourceWidth,
        int sourceHeight,
        Span<ushort> destination,
        int destinationStride,
        Point destinationPosition,
        int width,
        int height,
        int subsamplingX,
        int subsamplingY,
        int bitDepth,
        Av1GlobalMotionParameters parameters,
        Span<short> scratch)
        => PredictWarpedCompound<WarpedOperator>(
            source,
            sourceStride,
            sourceOrigin,
            sourceWidth,
            sourceHeight,
            destination,
            destinationStride,
            destinationPosition,
            width,
            height,
            subsamplingX,
            subsamplingY,
            bitDepth,
            parameters,
            scratch,
            useHardwareIntrinsics: true);

    /// <summary>
    /// Reconstructs one 8-bit warped block through a closed convolution operator.
    /// </summary>
    /// <typeparam name="TOperator">The convolution operator.</typeparam>
    /// <param name="source">The reference plane storage.</param>
    /// <param name="sourceStride">The distance between reference rows in samples.</param>
    /// <param name="sourceOrigin">The position of the top-left visible reference sample within <paramref name="source"/>.</param>
    /// <param name="sourceWidth">The visible reference width. Taps outside it read the nearest edge column.</param>
    /// <param name="sourceHeight">The visible reference height. Taps outside it read the nearest edge row.</param>
    /// <param name="destination">The prediction block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="destinationPosition">The plane position of the top-left block sample.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
    /// <param name="subsamplingX">The horizontal subsampling shift of the plane.</param>
    /// <param name="subsamplingY">The vertical subsampling shift of the plane.</param>
    /// <param name="parameters">The affine warp model and its shear parameters.</param>
    /// <param name="scratch">Signed intermediate storage of at least <see cref="WarpedScratchLength"/> elements.</param>
    /// <param name="useHardwareIntrinsics">Whether the SIMD kernels can run. False selects the scalar kernels.</param>
    private static void PredictWarped<TOperator>(
        ReadOnlySpan<byte> source,
        int sourceStride,
        Point sourceOrigin,
        int sourceWidth,
        int sourceHeight,
        Span<byte> destination,
        int destinationStride,
        Point destinationPosition,
        int width,
        int height,
        int subsamplingX,
        int subsamplingY,
        Av1GlobalMotionParameters parameters,
        Span<short> scratch,
        bool useHardwareIntrinsics)
        where TOperator : struct, IAv1WarpedPredictionOperator
    {
        ref byte sourceBase = ref MemoryMarshal.GetReference(source);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
        Span<ushort> intermediate = MemoryMarshal.Cast<short, ushort>(scratch)[..WarpedScratchLength];
        int horizontalBias = 1 << (8 + FilterBits - 1);
        int verticalBias = 1 << (8 + (2 * FilterBits) - Round0Bits);
        int verticalRound = (2 * FilterBits) - Round0Bits;

        for (int tileRow = destinationPosition.Y; tileRow < destinationPosition.Y + height; tileRow += WarpedTileSize)
        {
            for (int tileColumn = destinationPosition.X; tileColumn < destinationPosition.X + width; tileColumn += WarpedTileSize)
            {
                DeriveWarpedTilePosition(
                    parameters,
                    tileColumn,
                    tileRow,
                    subsamplingX,
                    subsamplingY,
                    out int integerX,
                    out int integerY,
                    out int phaseX,
                    out int phaseY);

                FilterWarpedHorizontal<TOperator>(
                    ref sourceBase,
                    sourceStride,
                    sourceOrigin,
                    sourceWidth,
                    sourceHeight,
                    integerX,
                    integerY,
                    phaseX,
                    parameters,
                    intermediate,
                    horizontalBias,
                    Round0Bits,
                    useHardwareIntrinsics);

                int tileHeight = Math.Min(WarpedTileSize, destinationPosition.Y + height - tileRow);
                int tileWidth = Math.Min(WarpedTileSize, destinationPosition.X + width - tileColumn);
                for (int row = 0; row < tileHeight; row++)
                {
                    int phase = phaseY + (parameters.Delta * row);
                    int destinationRowOffset = (tileRow - destinationPosition.Y + row) * destinationStride;
                    ref ushort intermediateSource = ref intermediate[row * WarpedTileSize];
                    ref byte destinationRow = ref Unsafe.Add(
                        ref destinationBase,
                        destinationRowOffset + tileColumn - destinationPosition.X);

                    FilterWarpedVertical<TOperator>(
                        ref intermediateSource,
                        ref destinationRow,
                        tileWidth,
                        phase,
                        parameters.Gamma,
                        verticalBias,
                        verticalRound,
                        useHardwareIntrinsics);
                }
            }
        }
    }

    /// <summary>
    /// Produces the unsigned horizontal intermediate tile for an 8-bit source plane.
    /// </summary>
    /// <typeparam name="TOperator">The convolution operator.</typeparam>
    /// <param name="sourceBase">The first element of the reference plane storage.</param>
    /// <param name="sourceStride">The distance between reference rows in samples.</param>
    /// <param name="sourceOrigin">The position of the top-left visible reference sample in the storage.</param>
    /// <param name="sourceWidth">The visible reference width.</param>
    /// <param name="sourceHeight">The visible reference height.</param>
    /// <param name="integerX">The integer reference column of the projected tile center.</param>
    /// <param name="integerY">The integer reference row of the projected tile center.</param>
    /// <param name="phaseX">The reduced horizontal phase of tile column zero. Each intermediate row adds its own shear offset.</param>
    /// <param name="parameters">The affine warp model and its shear parameters.</param>
    /// <param name="intermediate">Receives the 15 rows of 8 intermediate samples.</param>
    /// <param name="bias">The bias added to each sum before rounding.</param>
    /// <param name="round">The rounding shift of the horizontal pass.</param>
    /// <param name="useHardwareIntrinsics">Whether the SIMD kernels can run. False selects the scalar kernels.</param>
    private static void FilterWarpedHorizontal<TOperator>(
        ref byte sourceBase,
        int sourceStride,
        Point sourceOrigin,
        int sourceWidth,
        int sourceHeight,
        int integerX,
        int integerY,
        int phaseX,
        Av1GlobalMotionParameters parameters,
        Span<ushort> intermediate,
        int bias,
        int round,
        bool useHardwareIntrinsics)
        where TOperator : struct, IAv1WarpedPredictionOperator
    {
        // The AV1 specification clamps every horizontal tap to the frame: sample_x = clamp(ix + m, 0, width - 1).
        // A window inside the frame needs no clamp, so the common case uses one load per window.
        // The code handles three cases: all taps clamp to one edge column, a window crosses an edge, or no tap clamps.
        if (integerX <= -7 || integerX >= sourceWidth + 6)
        {
            // Every clamped tap is the same edge column. The eight taps of any phase sum to 1 << FilterBits, so the whole intermediate row holds one value.
            int edgeColumn = integerX <= -7 ? 0 : sourceWidth - 1;
            for (int row = -7; row < 8; row++)
            {
                int edgeY = Math.Clamp(integerY + row, 0, sourceHeight - 1);
                int sample = Unsafe.Add(ref sourceBase, ((sourceOrigin.Y + edgeY) * sourceStride) + sourceOrigin.X + edgeColumn);
                intermediate.Slice((row + 7) * WarpedTileSize, WarpedTileSize)
                    .Fill((ushort)RoundPowerOfTwoScalar(bias + (sample << FilterBits), round));
            }

            return;
        }

        // A tile across a vertical frame edge copies its window with the taps clamped.
        // The copy holds the same samples that the normative clamp of each tap selects.
        bool clampHorizontally = integerX - 7 < 0 || integerX + 9 > sourceWidth;
        Span<byte> window = stackalloc byte[clampHorizontally ? WarpedWindowLength : 0];
        for (int row = -7; row < 8; row++)
        {
            int sourceY = Math.Clamp(integerY + row, 0, sourceHeight - 1);
            int rowIndex = ((sourceOrigin.Y + sourceY) * sourceStride) + sourceOrigin.X;
            if (clampHorizontally)
            {
                ref byte clampedRow = ref Unsafe.Add(ref sourceBase, rowIndex);
                for (int tap = 0; tap < WarpedWindowLength; tap++)
                {
                    window[tap] = Unsafe.Add(ref clampedRow, Math.Clamp(integerX - 7 + tap, 0, sourceWidth - 1));
                }
            }

            ref byte sourceRow = ref clampHorizontally
                ? ref MemoryMarshal.GetReference(window)
                : ref Unsafe.Add(ref sourceBase, rowIndex + integerX - 7);

            ref ushort intermediateRow = ref intermediate[(row + 7) * WarpedTileSize];
            int phase = phaseX + (parameters.Beta * (row + 4));
            int column = 0;

            // The eight windows of a row use different warped phases. Each SIMD width packs complete eight-tap windows with their own coefficients.
            // Thus no buffer for each row is necessary.
            if (useHardwareIntrinsics && Vector512.IsHardwareAccelerated)
            {
                int oneVectorFromEnd = WarpedTileSize - 4;
                for (; column <= oneVectorFromEnd; column += 4)
                {
                    Vector128<int> sums = ConvolveWarpedVector512<TOperator>(
                        ref Unsafe.Add(ref sourceRow, column),
                        phase + (column * parameters.Alpha),
                        parameters.Alpha);

                    for (int lane = 0; lane < 4; lane++)
                    {
                        Unsafe.Add(ref intermediateRow, column + lane) =
                            (ushort)RoundPowerOfTwoScalar(bias + sums.GetElement(lane), round);
                    }
                }
            }

            if (useHardwareIntrinsics && Vector256.IsHardwareAccelerated)
            {
                int oneVectorFromEnd = WarpedTileSize - 2;
                for (; column <= oneVectorFromEnd; column += 2)
                {
                    Vector128<int> sums = ConvolveWarpedVector256<TOperator>(
                        ref Unsafe.Add(ref sourceRow, column),
                        phase + (column * parameters.Alpha),
                        parameters.Alpha);

                    Unsafe.Add(ref intermediateRow, column) = (ushort)RoundPowerOfTwoScalar(bias + sums.GetElement(0), round);
                    Unsafe.Add(ref intermediateRow, column + 1) = (ushort)RoundPowerOfTwoScalar(bias + sums.GetElement(1), round);
                }
            }

            if (useHardwareIntrinsics && Vector128.IsHardwareAccelerated)
            {
                for (; column < WarpedTileSize; column++)
                {
                    int sum = ConvolveWarpedVector128<TOperator>(
                        ref Unsafe.Add(ref sourceRow, column),
                        phase + (column * parameters.Alpha));

                    Unsafe.Add(ref intermediateRow, column) = (ushort)RoundPowerOfTwoScalar(bias + sum, round);
                }
            }

            for (; column < WarpedTileSize; column++)
            {
                ref short coefficients = ref GetWarpedFilterReference(phase + (column * parameters.Alpha));
                int sum = TOperator.Convolve(ref Unsafe.Add(ref sourceRow, column), 1, ref coefficients);
                Unsafe.Add(ref intermediateRow, column) = (ushort)RoundPowerOfTwoScalar(bias + sum, round);
            }
        }
    }

    /// <summary>
    /// Produces the unsigned horizontal intermediate tile for a high-bit-depth source plane.
    /// </summary>
    /// <typeparam name="TOperator">The convolution operator.</typeparam>
    /// <param name="sourceBase">The first element of the reference plane storage.</param>
    /// <param name="sourceStride">The distance between reference rows in samples.</param>
    /// <param name="sourceOrigin">The position of the top-left visible reference sample in the storage.</param>
    /// <param name="sourceWidth">The visible reference width.</param>
    /// <param name="sourceHeight">The visible reference height.</param>
    /// <param name="integerX">The integer reference column of the projected tile center.</param>
    /// <param name="integerY">The integer reference row of the projected tile center.</param>
    /// <param name="phaseX">The reduced horizontal phase of tile column zero. Each intermediate row adds its own shear offset.</param>
    /// <param name="parameters">The affine warp model and its shear parameters.</param>
    /// <param name="intermediate">Receives the 15 rows of 8 intermediate samples.</param>
    /// <param name="bias">The bias added to each sum before rounding.</param>
    /// <param name="round">The rounding shift of the horizontal pass.</param>
    /// <param name="useHardwareIntrinsics">Whether the SIMD kernels can run. False selects the scalar kernels.</param>
    private static void FilterWarpedHorizontal<TOperator>(
        ref ushort sourceBase,
        int sourceStride,
        Point sourceOrigin,
        int sourceWidth,
        int sourceHeight,
        int integerX,
        int integerY,
        int phaseX,
        Av1GlobalMotionParameters parameters,
        Span<ushort> intermediate,
        int bias,
        int round,
        bool useHardwareIntrinsics)
        where TOperator : struct, IAv1WarpedPredictionOperator
    {
        // The AV1 specification clamps every horizontal tap to the frame: sample_x = clamp(ix + m, 0, width - 1).
        // A window inside the frame needs no clamp, so the common case uses one load per window.
        // The code handles three cases: all taps clamp to one edge column, a window crosses an edge, or no tap clamps.
        if (integerX <= -7 || integerX >= sourceWidth + 6)
        {
            // Every clamped tap is the same edge column. The eight taps of any phase sum to 1 << FilterBits, so the whole intermediate row holds one value.
            int edgeColumn = integerX <= -7 ? 0 : sourceWidth - 1;
            for (int row = -7; row < 8; row++)
            {
                int edgeY = Math.Clamp(integerY + row, 0, sourceHeight - 1);
                int sample = Unsafe.Add(ref sourceBase, ((sourceOrigin.Y + edgeY) * sourceStride) + sourceOrigin.X + edgeColumn);
                intermediate.Slice((row + 7) * WarpedTileSize, WarpedTileSize)
                    .Fill((ushort)RoundPowerOfTwoScalar(bias + (sample << FilterBits), round));
            }

            return;
        }

        // A tile across a vertical frame edge copies its window with the taps clamped.
        // The copy holds the same samples that the normative clamp of each tap selects.
        bool clampHorizontally = integerX - 7 < 0 || integerX + 9 > sourceWidth;
        Span<ushort> window = stackalloc ushort[clampHorizontally ? WarpedWindowLength : 0];
        for (int row = -7; row < 8; row++)
        {
            int sourceY = Math.Clamp(integerY + row, 0, sourceHeight - 1);
            int rowIndex = ((sourceOrigin.Y + sourceY) * sourceStride) + sourceOrigin.X;
            if (clampHorizontally)
            {
                ref ushort clampedRow = ref Unsafe.Add(ref sourceBase, rowIndex);
                for (int tap = 0; tap < WarpedWindowLength; tap++)
                {
                    window[tap] = Unsafe.Add(ref clampedRow, Math.Clamp(integerX - 7 + tap, 0, sourceWidth - 1));
                }
            }

            ref ushort sourceRow = ref clampHorizontally
                ? ref MemoryMarshal.GetReference(window)
                : ref Unsafe.Add(ref sourceBase, rowIndex + integerX - 7);

            ref ushort intermediateRow = ref intermediate[(row + 7) * WarpedTileSize];
            int phase = phaseX + (parameters.Beta * (row + 4));
            int column = 0;

            if (useHardwareIntrinsics && Vector512.IsHardwareAccelerated)
            {
                int oneVectorFromEnd = WarpedTileSize - 4;
                for (; column <= oneVectorFromEnd; column += 4)
                {
                    Vector128<int> sums = ConvolveWarpedVector512<TOperator>(
                        ref Unsafe.Add(ref sourceRow, column),
                        1,
                        phase + (column * parameters.Alpha),
                        parameters.Alpha);

                    for (int lane = 0; lane < 4; lane++)
                    {
                        Unsafe.Add(ref intermediateRow, column + lane) =
                            (ushort)RoundPowerOfTwoScalar(bias + sums.GetElement(lane), round);
                    }
                }
            }

            if (useHardwareIntrinsics && Vector256.IsHardwareAccelerated)
            {
                int oneVectorFromEnd = WarpedTileSize - 2;
                for (; column <= oneVectorFromEnd; column += 2)
                {
                    Vector128<int> sums = ConvolveWarpedVector256<TOperator>(
                        ref Unsafe.Add(ref sourceRow, column),
                        1,
                        phase + (column * parameters.Alpha),
                        parameters.Alpha);

                    Unsafe.Add(ref intermediateRow, column) = (ushort)RoundPowerOfTwoScalar(bias + sums.GetElement(0), round);
                    Unsafe.Add(ref intermediateRow, column + 1) = (ushort)RoundPowerOfTwoScalar(bias + sums.GetElement(1), round);
                }
            }

            if (useHardwareIntrinsics && Vector128.IsHardwareAccelerated)
            {
                for (; column < WarpedTileSize; column++)
                {
                    int sum = ConvolveWarpedVector128<TOperator>(
                        ref Unsafe.Add(ref sourceRow, column),
                        1,
                        phase + (column * parameters.Alpha));

                    Unsafe.Add(ref intermediateRow, column) = (ushort)RoundPowerOfTwoScalar(bias + sum, round);
                }
            }

            for (; column < WarpedTileSize; column++)
            {
                ref short coefficients = ref GetWarpedFilterReference(phase + (column * parameters.Alpha));
                int sum = TOperator.Convolve(ref Unsafe.Add(ref sourceRow, column), 1, ref coefficients);
                Unsafe.Add(ref intermediateRow, column) = (ushort)RoundPowerOfTwoScalar(bias + sum, round);
            }
        }
    }

    /// <summary>
    /// Completes one 8-bit vertical warped-filter row.
    /// </summary>
    /// <typeparam name="TOperator">The convolution operator.</typeparam>
    /// <param name="source">The intermediate sample that the first vertical tap reads for column zero.</param>
    /// <param name="destination">The first sample of the destination row.</param>
    /// <param name="width">The number of output samples in the row.</param>
    /// <param name="phase">The reduced vertical phase of column zero.</param>
    /// <param name="phaseStep">The phase change from one column to the next.</param>
    /// <param name="bias">The bias added to each sum before rounding.</param>
    /// <param name="round">The rounding shift of the vertical pass.</param>
    /// <param name="useHardwareIntrinsics">Whether the SIMD kernels can run. False selects the scalar kernels.</param>
    private static void FilterWarpedVertical<TOperator>(
        ref ushort source,
        ref byte destination,
        int width,
        int phase,
        int phaseStep,
        int bias,
        int round,
        bool useHardwareIntrinsics)
        where TOperator : struct, IAv1WarpedPredictionOperator
    {
        int column = 0;
        if (useHardwareIntrinsics && Vector512.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = width - 4;
            for (; column <= oneVectorFromEnd; column += 4)
            {
                Vector128<int> sums = ConvolveWarpedVector512<TOperator>(
                    ref Unsafe.Add(ref source, column),
                    WarpedTileSize,
                    phase + (column * phaseStep),
                    phaseStep);

                for (int lane = 0; lane < 4; lane++)
                {
                    Unsafe.Add(ref destination, column + lane) = FinishWarpedByte(sums.GetElement(lane), bias, round);
                }
            }
        }

        if (useHardwareIntrinsics && Vector256.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = width - 2;
            for (; column <= oneVectorFromEnd; column += 2)
            {
                Vector128<int> sums = ConvolveWarpedVector256<TOperator>(
                    ref Unsafe.Add(ref source, column),
                    WarpedTileSize,
                    phase + (column * phaseStep),
                    phaseStep);

                Unsafe.Add(ref destination, column) = FinishWarpedByte(sums.GetElement(0), bias, round);
                Unsafe.Add(ref destination, column + 1) = FinishWarpedByte(sums.GetElement(1), bias, round);
            }
        }

        if (useHardwareIntrinsics && Vector128.IsHardwareAccelerated)
        {
            for (; column < width; column++)
            {
                int sum = ConvolveWarpedVector128<TOperator>(
                    ref Unsafe.Add(ref source, column),
                    WarpedTileSize,
                    phase + (column * phaseStep));

                Unsafe.Add(ref destination, column) = FinishWarpedByte(sum, bias, round);
            }
        }

        for (; column < width; column++)
        {
            ref short coefficients = ref GetWarpedFilterReference(phase + (column * phaseStep));
            int sum = TOperator.Convolve(ref Unsafe.Add(ref source, column), WarpedTileSize, ref coefficients);
            Unsafe.Add(ref destination, column) = FinishWarpedByte(sum, bias, round);
        }
    }

    /// <summary>
    /// Completes one compound-intermediate vertical warped-filter row.
    /// </summary>
    /// <typeparam name="TOperator">The convolution operator.</typeparam>
    /// <param name="source">The intermediate sample that the first vertical tap reads for column zero.</param>
    /// <param name="destination">The first sample of the destination row.</param>
    /// <param name="width">The number of output samples in the row.</param>
    /// <param name="phase">The reduced vertical phase of column zero.</param>
    /// <param name="phaseStep">The phase change from one column to the next.</param>
    /// <param name="bias">The bias added to each sum before rounding.</param>
    /// <param name="useHardwareIntrinsics">Whether the SIMD kernels can run. False selects the scalar kernels.</param>
    private static void FilterWarpedCompoundVertical<TOperator>(
        ref ushort source,
        ref ushort destination,
        int width,
        int phase,
        int phaseStep,
        int bias,
        bool useHardwareIntrinsics)
        where TOperator : struct, IAv1WarpedPredictionOperator
    {
        int column = 0;
        if (useHardwareIntrinsics && Vector512.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = width - 4;
            for (; column <= oneVectorFromEnd; column += 4)
            {
                Vector128<int> sums = ConvolveWarpedVector512<TOperator>(
                    ref Unsafe.Add(ref source, column),
                    WarpedTileSize,
                    phase + (column * phaseStep),
                    phaseStep);

                for (int lane = 0; lane < 4; lane++)
                {
                    Unsafe.Add(ref destination, column + lane) = FinishWarpedCompound(sums.GetElement(lane), bias);
                }
            }
        }

        if (useHardwareIntrinsics && Vector256.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = width - 2;
            for (; column <= oneVectorFromEnd; column += 2)
            {
                Vector128<int> sums = ConvolveWarpedVector256<TOperator>(
                    ref Unsafe.Add(ref source, column),
                    WarpedTileSize,
                    phase + (column * phaseStep),
                    phaseStep);

                Unsafe.Add(ref destination, column) = FinishWarpedCompound(sums.GetElement(0), bias);
                Unsafe.Add(ref destination, column + 1) = FinishWarpedCompound(sums.GetElement(1), bias);
            }
        }

        if (useHardwareIntrinsics && Vector128.IsHardwareAccelerated)
        {
            for (; column < width; column++)
            {
                int sum = ConvolveWarpedVector128<TOperator>(
                    ref Unsafe.Add(ref source, column),
                    WarpedTileSize,
                    phase + (column * phaseStep));

                Unsafe.Add(ref destination, column) = FinishWarpedCompound(sum, bias);
            }
        }

        for (; column < width; column++)
        {
            ref short coefficients = ref GetWarpedFilterReference(phase + (column * phaseStep));
            int sum = TOperator.Convolve(ref Unsafe.Add(ref source, column), WarpedTileSize, ref coefficients);
            Unsafe.Add(ref destination, column) = FinishWarpedCompound(sum, bias);
        }
    }

    /// <summary>
    /// Completes one high-bit-depth vertical warped-filter row.
    /// </summary>
    /// <typeparam name="TOperator">The convolution operator.</typeparam>
    /// <param name="source">The intermediate sample that the first vertical tap reads for column zero.</param>
    /// <param name="destination">The first sample of the destination row.</param>
    /// <param name="width">The number of output samples in the row.</param>
    /// <param name="phase">The reduced vertical phase of column zero.</param>
    /// <param name="phaseStep">The phase change from one column to the next.</param>
    /// <param name="bias">The bias added to each sum before rounding.</param>
    /// <param name="round">The rounding shift of the vertical pass.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="maximum">The largest sample value of the bit depth.</param>
    /// <param name="useHardwareIntrinsics">Whether the SIMD kernels can run. False selects the scalar kernels.</param>
    private static void FilterWarpedVertical<TOperator>(
        ref ushort source,
        ref ushort destination,
        int width,
        int phase,
        int phaseStep,
        int bias,
        int round,
        int bitDepth,
        int maximum,
        bool useHardwareIntrinsics)
        where TOperator : struct, IAv1WarpedPredictionOperator
    {
        int column = 0;
        if (useHardwareIntrinsics && Vector512.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = width - 4;
            for (; column <= oneVectorFromEnd; column += 4)
            {
                Vector128<int> sums = ConvolveWarpedVector512<TOperator>(
                    ref Unsafe.Add(ref source, column),
                    WarpedTileSize,
                    phase + (column * phaseStep),
                    phaseStep);

                for (int lane = 0; lane < 4; lane++)
                {
                    Unsafe.Add(ref destination, column + lane) = FinishWarpedHighBitDepth(sums.GetElement(lane), bias, round, bitDepth, maximum);
                }
            }
        }

        if (useHardwareIntrinsics && Vector256.IsHardwareAccelerated)
        {
            int oneVectorFromEnd = width - 2;
            for (; column <= oneVectorFromEnd; column += 2)
            {
                Vector128<int> sums = ConvolveWarpedVector256<TOperator>(
                    ref Unsafe.Add(ref source, column),
                    WarpedTileSize,
                    phase + (column * phaseStep),
                    phaseStep);

                Unsafe.Add(ref destination, column) = FinishWarpedHighBitDepth(sums.GetElement(0), bias, round, bitDepth, maximum);
                Unsafe.Add(ref destination, column + 1) = FinishWarpedHighBitDepth(sums.GetElement(1), bias, round, bitDepth, maximum);
            }
        }

        if (useHardwareIntrinsics && Vector128.IsHardwareAccelerated)
        {
            for (; column < width; column++)
            {
                int sum = ConvolveWarpedVector128<TOperator>(
                    ref Unsafe.Add(ref source, column),
                    WarpedTileSize,
                    phase + (column * phaseStep));

                Unsafe.Add(ref destination, column) = FinishWarpedHighBitDepth(sum, bias, round, bitDepth, maximum);
            }
        }

        for (; column < width; column++)
        {
            ref short coefficients = ref GetWarpedFilterReference(phase + (column * phaseStep));
            int sum = TOperator.Convolve(ref Unsafe.Add(ref source, column), WarpedTileSize, ref coefficients);
            Unsafe.Add(ref destination, column) = FinishWarpedHighBitDepth(sum, bias, round, bitDepth, maximum);
        }
    }

    /// <summary>
    /// Convolves four adjacent 8-bit source windows with independent phases.
    /// </summary>
    /// <typeparam name="TOperator">The convolution operator.</typeparam>
    /// <param name="source">The first sample of the first window. Each next window starts one sample later.</param>
    /// <param name="phase">The reduced phase of the first window.</param>
    /// <param name="phaseStep">The phase change from one window to the next.</param>
    /// <returns>The four dot products.</returns>
    private static Vector128<int> ConvolveWarpedVector512<TOperator>(ref byte source, int phase, int phaseStep)
        where TOperator : struct, IAv1WarpedPredictionOperator
    {
        Vector256<ushort> sampleLower = Vector256.Create(LoadWarpedWindow(ref source), LoadWarpedWindow(ref Unsafe.Add(ref source, 1)));
        Vector256<ushort> sampleUpper = Vector256.Create(LoadWarpedWindow(ref Unsafe.Add(ref source, 2)), LoadWarpedWindow(ref Unsafe.Add(ref source, 3)));
        Vector256<short> coefficientLower = Vector256.Create(LoadWarpedCoefficients(phase), LoadWarpedCoefficients(phase + phaseStep));
        Vector256<short> coefficientUpper = Vector256.Create(LoadWarpedCoefficients(phase + (2 * phaseStep)), LoadWarpedCoefficients(phase + (3 * phaseStep)));
        return TOperator.Convolve(Vector512.Create(sampleLower, sampleUpper), Vector512.Create(coefficientLower, coefficientUpper));
    }

    /// <summary>
    /// Convolves two adjacent 8-bit source windows with independent phases.
    /// </summary>
    /// <typeparam name="TOperator">The convolution operator.</typeparam>
    /// <param name="source">The first sample of the first window. The second window starts one sample later.</param>
    /// <param name="phase">The reduced phase of the first window.</param>
    /// <param name="phaseStep">The phase change from one window to the next.</param>
    /// <returns>The two dot products in the low lanes.</returns>
    private static Vector128<int> ConvolveWarpedVector256<TOperator>(ref byte source, int phase, int phaseStep)
        where TOperator : struct, IAv1WarpedPredictionOperator
        => TOperator.Convolve(
            Vector256.Create(LoadWarpedWindow(ref source), LoadWarpedWindow(ref Unsafe.Add(ref source, 1))),
            Vector256.Create(LoadWarpedCoefficients(phase), LoadWarpedCoefficients(phase + phaseStep)));

    /// <summary>
    /// Convolves one 8-bit source window.
    /// </summary>
    /// <typeparam name="TOperator">The convolution operator.</typeparam>
    /// <param name="source">The first sample of the window.</param>
    /// <param name="phase">The reduced phase of the window.</param>
    /// <returns>The dot product.</returns>
    private static int ConvolveWarpedVector128<TOperator>(ref byte source, int phase)
        where TOperator : struct, IAv1WarpedPredictionOperator
        => TOperator.Convolve(LoadWarpedWindow(ref source), LoadWarpedCoefficients(phase));

    /// <summary>
    /// Convolves four adjacent high-bit-depth source windows with independent phases.
    /// </summary>
    /// <typeparam name="TOperator">The convolution operator.</typeparam>
    /// <param name="source">The first sample of the first window. Each next window starts one sample later.</param>
    /// <param name="sourceStride">The distance between the samples of one window: one for a row, or the tile width for a column.</param>
    /// <param name="phase">The reduced phase of the first window.</param>
    /// <param name="phaseStep">The phase change from one window to the next.</param>
    /// <returns>The four dot products.</returns>
    private static Vector128<int> ConvolveWarpedVector512<TOperator>(ref ushort source, int sourceStride, int phase, int phaseStep)
        where TOperator : struct, IAv1WarpedPredictionOperator
    {
        Vector256<ushort> sampleLower = Vector256.Create(LoadWarpedWindow(ref source, sourceStride), LoadWarpedWindow(ref Unsafe.Add(ref source, 1), sourceStride));
        Vector256<ushort> sampleUpper = Vector256.Create(LoadWarpedWindow(ref Unsafe.Add(ref source, 2), sourceStride), LoadWarpedWindow(ref Unsafe.Add(ref source, 3), sourceStride));
        Vector256<short> coefficientLower = Vector256.Create(LoadWarpedCoefficients(phase), LoadWarpedCoefficients(phase + phaseStep));
        Vector256<short> coefficientUpper = Vector256.Create(LoadWarpedCoefficients(phase + (2 * phaseStep)), LoadWarpedCoefficients(phase + (3 * phaseStep)));
        return TOperator.Convolve(Vector512.Create(sampleLower, sampleUpper), Vector512.Create(coefficientLower, coefficientUpper));
    }

    /// <summary>
    /// Convolves two adjacent high-bit-depth source windows with independent phases.
    /// </summary>
    /// <typeparam name="TOperator">The convolution operator.</typeparam>
    /// <param name="source">The first sample of the first window. The second window starts one sample later.</param>
    /// <param name="sourceStride">The distance between the samples of one window: one for a row, or the tile width for a column.</param>
    /// <param name="phase">The reduced phase of the first window.</param>
    /// <param name="phaseStep">The phase change from one window to the next.</param>
    /// <returns>The two dot products in the low lanes.</returns>
    private static Vector128<int> ConvolveWarpedVector256<TOperator>(ref ushort source, int sourceStride, int phase, int phaseStep)
        where TOperator : struct, IAv1WarpedPredictionOperator
        => TOperator.Convolve(
            Vector256.Create(LoadWarpedWindow(ref source, sourceStride), LoadWarpedWindow(ref Unsafe.Add(ref source, 1), sourceStride)),
            Vector256.Create(LoadWarpedCoefficients(phase), LoadWarpedCoefficients(phase + phaseStep)));

    /// <summary>
    /// Convolves one high-bit-depth source window.
    /// </summary>
    /// <typeparam name="TOperator">The convolution operator.</typeparam>
    /// <param name="source">The first sample of the window.</param>
    /// <param name="sourceStride">The distance between the samples of the window: one for a row, or the tile width for a column.</param>
    /// <param name="phase">The reduced phase of the window.</param>
    /// <returns>The dot product.</returns>
    private static int ConvolveWarpedVector128<TOperator>(ref ushort source, int sourceStride, int phase)
        where TOperator : struct, IAv1WarpedPredictionOperator
        => TOperator.Convolve(LoadWarpedWindow(ref source, sourceStride), LoadWarpedCoefficients(phase));

    /// <summary>
    /// Loads eight adjacent unsigned byte samples as unsigned 16-bit lanes.
    /// </summary>
    /// <param name="source">The first sample of the window.</param>
    /// <returns>The widened samples.</returns>
    private static Vector128<ushort> LoadWarpedWindow(ref byte source)
    {
        // The load reads sixteen bytes. The widen keeps only the lower eight, which are the window.
        Vector128<byte> packed = Vector128.LoadUnsafe(ref source);
        return Vector128.Widen(packed).Lower;
    }

    /// <summary>
    /// Loads eight unsigned high-bit-depth samples with the supplied spacing.
    /// </summary>
    /// <param name="source">The first sample of the window.</param>
    /// <param name="sourceStride">The distance between the samples of the window. One selects a single contiguous load.</param>
    /// <returns>The samples.</returns>
    private static Vector128<ushort> LoadWarpedWindow(ref ushort source, int sourceStride)
    {
        if (sourceStride == 1)
        {
            return Vector128.LoadUnsafe(ref source);
        }

        return Vector128.Create(
            source,
            Unsafe.Add(ref source, sourceStride),
            Unsafe.Add(ref source, sourceStride * 2),
            Unsafe.Add(ref source, sourceStride * 3),
            Unsafe.Add(ref source, sourceStride * 4),
            Unsafe.Add(ref source, sourceStride * 5),
            Unsafe.Add(ref source, sourceStride * 6),
            Unsafe.Add(ref source, sourceStride * 7));
    }

    /// <summary>
    /// Loads one signed Q7 warped-filter phase.
    /// </summary>
    /// <param name="phase">The reduced phase.</param>
    /// <returns>The eight coefficients.</returns>
    private static Vector128<short> LoadWarpedCoefficients(int phase)
        => Vector128.LoadUnsafe(ref GetWarpedFilterReference(phase));

    /// <summary>
    /// Applies the final 8-bit warped-prediction rounding and clipping.
    /// </summary>
    /// <param name="sum">The vertical dot product.</param>
    /// <param name="bias">The vertical bias.</param>
    /// <param name="round">The rounding shift of the vertical pass.</param>
    /// <returns>The clipped sample.</returns>
    private static byte FinishWarpedByte(int sum, int bias, int round)
    {
        // After the shift, the vertical and horizontal biases are 2^8 and 2^7. The subtraction removes both.
        int value = RoundPowerOfTwoScalar(bias + sum, round) - (1 << 7) - (1 << 8);
        return (byte)Math.Clamp(value, byte.MinValue, byte.MaxValue);
    }

    /// <summary>
    /// Applies the compound-intermediate warped-prediction rounding. The result keeps the biases for the compound blend.
    /// </summary>
    /// <param name="sum">The vertical dot product.</param>
    /// <param name="bias">The vertical bias.</param>
    /// <returns>The unsigned compound intermediate.</returns>
    private static ushort FinishWarpedCompound(int sum, int bias)
        => (ushort)RoundPowerOfTwoScalar(bias + sum, Av1CompoundInterPredictor.CompoundRound1Bits);

    /// <summary>
    /// Applies the final high-bit-depth warped-prediction rounding and clipping.
    /// </summary>
    /// <param name="sum">The vertical dot product.</param>
    /// <param name="bias">The vertical bias.</param>
    /// <param name="round">The rounding shift of the vertical pass.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="maximum">The largest sample value of the bit depth.</param>
    /// <returns>The clipped sample.</returns>
    private static ushort FinishWarpedHighBitDepth(int sum, int bias, int round, int bitDepth, int maximum)
    {
        // After the shift, the vertical and horizontal biases are 2^bitDepth and 2^(bitDepth - 1). The subtraction removes both.
        int value = RoundPowerOfTwoScalar(bias + sum, round) - (1 << (bitDepth - 1)) - (1 << bitDepth);
        return (ushort)Math.Clamp(value, 0, maximum);
    }

    /// <summary>
    /// Reconstructs one 8-bit warped reference without discarding the compound convolution precision.
    /// </summary>
    /// <typeparam name="TOperator">The convolution operator.</typeparam>
    /// <param name="source">The reference plane storage.</param>
    /// <param name="sourceStride">The distance between reference rows in samples.</param>
    /// <param name="sourceOrigin">The position of the top-left visible reference sample within <paramref name="source"/>.</param>
    /// <param name="sourceWidth">The visible reference width. Taps outside it read the nearest edge column.</param>
    /// <param name="sourceHeight">The visible reference height. Taps outside it read the nearest edge row.</param>
    /// <param name="destination">The compound intermediate destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="destinationPosition">The plane position of the top-left block sample.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
    /// <param name="subsamplingX">The horizontal subsampling shift of the plane.</param>
    /// <param name="subsamplingY">The vertical subsampling shift of the plane.</param>
    /// <param name="parameters">The affine warp model and its shear parameters.</param>
    /// <param name="scratch">Signed intermediate storage of at least <see cref="WarpedScratchLength"/> elements.</param>
    /// <param name="useHardwareIntrinsics">Whether the SIMD kernels can run. False selects the scalar kernels.</param>
    private static void PredictWarpedCompound<TOperator>(
        ReadOnlySpan<byte> source,
        int sourceStride,
        Point sourceOrigin,
        int sourceWidth,
        int sourceHeight,
        Span<ushort> destination,
        int destinationStride,
        Point destinationPosition,
        int width,
        int height,
        int subsamplingX,
        int subsamplingY,
        Av1GlobalMotionParameters parameters,
        Span<short> scratch,
        bool useHardwareIntrinsics)
        where TOperator : struct, IAv1WarpedPredictionOperator
    {
        ref byte sourceBase = ref MemoryMarshal.GetReference(source);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        Span<ushort> intermediate = MemoryMarshal.Cast<short, ushort>(scratch)[..WarpedScratchLength];
        int horizontalBias = 1 << (8 + FilterBits - 1);
        int verticalBias = 1 << (8 + (2 * FilterBits) - Round0Bits);

        for (int tileRow = destinationPosition.Y; tileRow < destinationPosition.Y + height; tileRow += WarpedTileSize)
        {
            for (int tileColumn = destinationPosition.X; tileColumn < destinationPosition.X + width; tileColumn += WarpedTileSize)
            {
                DeriveWarpedTilePosition(
                    parameters,
                    tileColumn,
                    tileRow,
                    subsamplingX,
                    subsamplingY,
                    out int integerX,
                    out int integerY,
                    out int phaseX,
                    out int phaseY);

                FilterWarpedHorizontal<TOperator>(
                    ref sourceBase,
                    sourceStride,
                    sourceOrigin,
                    sourceWidth,
                    sourceHeight,
                    integerX,
                    integerY,
                    phaseX,
                    parameters,
                    intermediate,
                    horizontalBias,
                    Round0Bits,
                    useHardwareIntrinsics);

                int tileHeight = Math.Min(WarpedTileSize, destinationPosition.Y + height - tileRow);
                int tileWidth = Math.Min(WarpedTileSize, destinationPosition.X + width - tileColumn);
                for (int row = 0; row < tileHeight; row++)
                {
                    int phase = phaseY + (parameters.Delta * row);
                    int destinationRowOffset = (tileRow - destinationPosition.Y + row) * destinationStride;
                    ref ushort intermediateSource = ref intermediate[row * WarpedTileSize];
                    ref ushort destinationRow = ref Unsafe.Add(
                        ref destinationBase,
                        destinationRowOffset + tileColumn - destinationPosition.X);

                    FilterWarpedCompoundVertical<TOperator>(
                        ref intermediateSource,
                        ref destinationRow,
                        tileWidth,
                        phase,
                        parameters.Gamma,
                        verticalBias,
                        useHardwareIntrinsics);
                }
            }
        }
    }

    /// <summary>
    /// Reconstructs one high-bit-depth warped reference without discarding the compound convolution precision.
    /// </summary>
    /// <typeparam name="TOperator">The convolution operator.</typeparam>
    /// <param name="source">The reference plane storage.</param>
    /// <param name="sourceStride">The distance between reference rows in samples.</param>
    /// <param name="sourceOrigin">The position of the top-left visible reference sample within <paramref name="source"/>.</param>
    /// <param name="sourceWidth">The visible reference width. Taps outside it read the nearest edge column.</param>
    /// <param name="sourceHeight">The visible reference height. Taps outside it read the nearest edge row.</param>
    /// <param name="destination">The compound intermediate destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="destinationPosition">The plane position of the top-left block sample.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
    /// <param name="subsamplingX">The horizontal subsampling shift of the plane.</param>
    /// <param name="subsamplingY">The vertical subsampling shift of the plane.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="parameters">The affine warp model and its shear parameters.</param>
    /// <param name="scratch">Signed intermediate storage of at least <see cref="WarpedScratchLength"/> elements.</param>
    /// <param name="useHardwareIntrinsics">Whether the SIMD kernels can run. False selects the scalar kernels.</param>
    private static void PredictWarpedCompound<TOperator>(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        Point sourceOrigin,
        int sourceWidth,
        int sourceHeight,
        Span<ushort> destination,
        int destinationStride,
        Point destinationPosition,
        int width,
        int height,
        int subsamplingX,
        int subsamplingY,
        int bitDepth,
        Av1GlobalMotionParameters parameters,
        Span<short> scratch,
        bool useHardwareIntrinsics)
        where TOperator : struct, IAv1WarpedPredictionOperator
    {
        ref ushort sourceBase = ref MemoryMarshal.GetReference(source);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        Span<ushort> intermediate = MemoryMarshal.Cast<short, ushort>(scratch)[..WarpedScratchLength];

        // For 12-bit input, round0 grows so that every biased horizontal sample fits in the 16-bit scratch tile.
        // Compound prediction keeps round1 at 7. The final blend removes the remaining bits of the Q14 shift.
        int intermediateRange = bitDepth + FilterBits - Round0Bits + 2;
        int round0 = Round0Bits + Math.Max(intermediateRange - 16, 0);
        int horizontalBias = 1 << (bitDepth + FilterBits - 1);
        int verticalBias = 1 << (bitDepth + (2 * FilterBits) - round0);

        for (int tileRow = destinationPosition.Y; tileRow < destinationPosition.Y + height; tileRow += WarpedTileSize)
        {
            for (int tileColumn = destinationPosition.X; tileColumn < destinationPosition.X + width; tileColumn += WarpedTileSize)
            {
                DeriveWarpedTilePosition(
                    parameters,
                    tileColumn,
                    tileRow,
                    subsamplingX,
                    subsamplingY,
                    out int integerX,
                    out int integerY,
                    out int phaseX,
                    out int phaseY);

                FilterWarpedHorizontal<TOperator>(
                    ref sourceBase,
                    sourceStride,
                    sourceOrigin,
                    sourceWidth,
                    sourceHeight,
                    integerX,
                    integerY,
                    phaseX,
                    parameters,
                    intermediate,
                    horizontalBias,
                    round0,
                    useHardwareIntrinsics);

                int tileHeight = Math.Min(WarpedTileSize, destinationPosition.Y + height - tileRow);
                int tileWidth = Math.Min(WarpedTileSize, destinationPosition.X + width - tileColumn);
                for (int row = 0; row < tileHeight; row++)
                {
                    int phase = phaseY + (parameters.Delta * row);
                    int destinationRowOffset = (tileRow - destinationPosition.Y + row) * destinationStride;
                    ref ushort intermediateSource = ref intermediate[row * WarpedTileSize];
                    ref ushort destinationRow = ref Unsafe.Add(
                        ref destinationBase,
                        destinationRowOffset + tileColumn - destinationPosition.X);

                    FilterWarpedCompoundVertical<TOperator>(
                        ref intermediateSource,
                        ref destinationRow,
                        tileWidth,
                        phase,
                        parameters.Gamma,
                        verticalBias,
                        useHardwareIntrinsics);
                }
            }
        }
    }

    /// <summary>
    /// Reconstructs one high-bit-depth warped block through a closed convolution operator.
    /// </summary>
    /// <typeparam name="TOperator">The convolution operator.</typeparam>
    /// <param name="source">The reference plane storage.</param>
    /// <param name="sourceStride">The distance between reference rows in samples.</param>
    /// <param name="sourceOrigin">The position of the top-left visible reference sample within <paramref name="source"/>.</param>
    /// <param name="sourceWidth">The visible reference width. Taps outside it read the nearest edge column.</param>
    /// <param name="sourceHeight">The visible reference height. Taps outside it read the nearest edge row.</param>
    /// <param name="destination">The prediction block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="destinationPosition">The plane position of the top-left block sample.</param>
    /// <param name="width">The block width in samples.</param>
    /// <param name="height">The block height in samples.</param>
    /// <param name="subsamplingX">The horizontal subsampling shift of the plane.</param>
    /// <param name="subsamplingY">The vertical subsampling shift of the plane.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="parameters">The affine warp model and its shear parameters.</param>
    /// <param name="scratch">Signed intermediate storage of at least <see cref="WarpedScratchLength"/> elements.</param>
    /// <param name="useHardwareIntrinsics">Whether the SIMD kernels can run. False selects the scalar kernels.</param>
    private static void PredictWarped<TOperator>(
        ReadOnlySpan<ushort> source,
        int sourceStride,
        Point sourceOrigin,
        int sourceWidth,
        int sourceHeight,
        Span<ushort> destination,
        int destinationStride,
        Point destinationPosition,
        int width,
        int height,
        int subsamplingX,
        int subsamplingY,
        int bitDepth,
        Av1GlobalMotionParameters parameters,
        Span<short> scratch,
        bool useHardwareIntrinsics)
        where TOperator : struct, IAv1WarpedPredictionOperator
    {
        ref ushort sourceBase = ref MemoryMarshal.GetReference(source);
        ref ushort destinationBase = ref MemoryMarshal.GetReference(destination);
        Span<ushort> intermediate = MemoryMarshal.Cast<short, ushort>(scratch)[..WarpedScratchLength];

        // For 12-bit prediction, round0 grows by two so that the biased horizontal intermediate fits in 16 bits.
        // round1 shrinks by the same amount, so the total shift stays Q14.
        int intermediateRange = bitDepth + FilterBits - Round0Bits + 2;
        int round0 = Round0Bits + Math.Max(intermediateRange - 16, 0);
        int verticalRound = (2 * FilterBits) - round0;
        int horizontalBias = 1 << (bitDepth + FilterBits - 1);
        int verticalBias = 1 << (bitDepth + (2 * FilterBits) - round0);
        int maximum = (1 << bitDepth) - 1;

        for (int tileRow = destinationPosition.Y; tileRow < destinationPosition.Y + height; tileRow += WarpedTileSize)
        {
            for (int tileColumn = destinationPosition.X; tileColumn < destinationPosition.X + width; tileColumn += WarpedTileSize)
            {
                DeriveWarpedTilePosition(
                    parameters,
                    tileColumn,
                    tileRow,
                    subsamplingX,
                    subsamplingY,
                    out int integerX,
                    out int integerY,
                    out int phaseX,
                    out int phaseY);

                FilterWarpedHorizontal<TOperator>(
                    ref sourceBase,
                    sourceStride,
                    sourceOrigin,
                    sourceWidth,
                    sourceHeight,
                    integerX,
                    integerY,
                    phaseX,
                    parameters,
                    intermediate,
                    horizontalBias,
                    round0,
                    useHardwareIntrinsics);

                int tileHeight = Math.Min(WarpedTileSize, destinationPosition.Y + height - tileRow);
                int tileWidth = Math.Min(WarpedTileSize, destinationPosition.X + width - tileColumn);
                for (int row = 0; row < tileHeight; row++)
                {
                    int phase = phaseY + (parameters.Delta * row);
                    int destinationRowOffset = (tileRow - destinationPosition.Y + row) * destinationStride;
                    ref ushort intermediateSource = ref intermediate[row * WarpedTileSize];
                    ref ushort destinationRow = ref Unsafe.Add(
                        ref destinationBase,
                        destinationRowOffset + tileColumn - destinationPosition.X);

                    FilterWarpedVertical<TOperator>(
                        ref intermediateSource,
                        ref destinationRow,
                        tileWidth,
                        phase,
                        parameters.Gamma,
                        verticalBias,
                        verticalRound,
                        bitDepth,
                        maximum,
                        useHardwareIntrinsics);
                }
            }
        }
    }

    /// <summary>
    /// Projects the center of one 8x8 output tile and derives its integer source position and reduced phases.
    /// </summary>
    /// <param name="parameters">The affine warp model and its shear parameters.</param>
    /// <param name="tileColumn">The plane column of the top-left tile sample.</param>
    /// <param name="tileRow">The plane row of the top-left tile sample.</param>
    /// <param name="subsamplingX">The horizontal subsampling shift of the plane.</param>
    /// <param name="subsamplingY">The vertical subsampling shift of the plane.</param>
    /// <param name="integerX">Receives the integer reference column of the projected tile center.</param>
    /// <param name="integerY">Receives the integer reference row of the projected tile center.</param>
    /// <param name="phaseX">Receives the reduced horizontal phase.</param>
    /// <param name="phaseY">Receives the reduced vertical phase.</param>
    private static void DeriveWarpedTilePosition(
        Av1GlobalMotionParameters parameters,
        int tileColumn,
        int tileRow,
        int subsamplingX,
        int subsamplingY,
        out int integerX,
        out int integerY,
        out int phaseX,
        out int phaseY)
    {
        // The model projects the tile center in luma coordinates. The shift by the subsampling returns the result to the plane.
        int sourceX = (tileColumn + 4) << subsamplingX;
        int sourceY = (tileRow + 4) << subsamplingY;
        long projectedX = ((long)parameters[2] * sourceX) + ((long)parameters[3] * sourceY) + parameters[0];
        long projectedY = ((long)parameters[4] * sourceX) + ((long)parameters[5] * sourceY) + parameters[1];
        long planeX = projectedX >> subsamplingX;
        long planeY = projectedY >> subsamplingY;
        integerX = (int)(planeX >> Av1GlobalMotionParameters.ModelPrecisionBits);
        integerY = (int)(planeY >> Av1GlobalMotionParameters.ModelPrecisionBits);
        phaseX = (int)planeX & (Av1GlobalMotionParameters.ModelScale - 1);
        phaseY = (int)planeY & (Av1GlobalMotionParameters.ModelScale - 1);

        // The offsets of four shear steps move the phases from the tile center to the first column and the first row.
        phaseX += (-4 * parameters.Alpha) + (-4 * parameters.Beta);
        phaseY += (-4 * parameters.Gamma) + (-4 * parameters.Delta);

        // The shear parameters are multiples of 64 model units. The phases clear the same six low bits after the projection of the tile center.
        // This keeps negative and positive phases on the grid of the filter table.
        phaseX &= -1 << 6;
        phaseY &= -1 << 6;
    }

    /// <summary>
    /// Gets a reference to the first coefficient for one reduced warped-filter phase.
    /// </summary>
    /// <param name="phase">The reduced phase in model units.</param>
    /// <returns>A reference to the first of the eight coefficients.</returns>
    private static ref short GetWarpedFilterReference(int phase)
    {
        // The phase rounds to 1/64 sample. The table starts at -1 sample, so index 64 is phase zero.
        int filterIndex = ((phase + (1 << (WarpedDifferencePrecisionBits - 1))) >> WarpedDifferencePrecisionBits) +
            WarpedPixelPrecisionShifts;

        return ref Unsafe.Add(ref MemoryMarshal.GetReference(WarpedFilter), filterIndex * FilterCoefficientCount);
    }

    /// <summary>
    /// Divides a nonnegative value by a power of two with nearest-integer rounding.
    /// </summary>
    /// <param name="value">The nonnegative value.</param>
    /// <param name="bitCount">The shift. It must be at least one.</param>
    /// <returns>The rounded quotient.</returns>
    private static int RoundPowerOfTwoScalar(int value, int bitCount)
        => (value + (1 << (bitCount - 1))) >> bitCount;
}
