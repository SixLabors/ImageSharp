// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1InterPredictor;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Reconstructs local and global affine warped-motion prediction blocks.
/// </content>
internal static partial class Av1WarpedInterPredictor
{
    /// <summary>
    /// The number of rows in one warped filter's horizontal intermediate tile.
    /// </summary>
    private const int WarpedIntermediateRows = 15;

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
    /// Gets the number of signed 16-bit elements required by warped prediction.
    /// </summary>
    public const int WarpedScratchLength = WarpedIntermediateRows * WarpedTileSize;

    /// <summary>
    /// Reconstructs an 8-bit affine warped prediction using the widest supported convolution operator.
    /// </summary>
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
    /// Reconstructs an 8-bit affine warped reference into AV1's unsigned compound intermediate format.
    /// </summary>
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
    /// Reconstructs a high-bit-depth affine warped prediction using the widest supported convolution operator.
    /// </summary>
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
    /// Reconstructs an 8-bit affine warped prediction without explicit hardware intrinsics.
    /// </summary>
    public static void PredictWarpedScalar(
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
            useHardwareIntrinsics: false);

    /// <summary>
    /// Reconstructs an 8-bit affine warped reference into compound intermediates without explicit hardware intrinsics.
    /// </summary>
    public static void PredictWarpedCompoundScalar(
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
            useHardwareIntrinsics: false);

    /// <summary>
    /// Reconstructs a high-bit-depth affine warped prediction without explicit hardware intrinsics.
    /// </summary>
    public static void PredictWarpedScalar(
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
            useHardwareIntrinsics: false);

    /// <summary>
    /// Reconstructs one 8-bit warped block through a closed convolution operator.
    /// </summary>
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
    private static void FilterWarpedHorizontal<TOperator>(
        ref byte sourceBase,
        int sourceStride,
        Point sourceOrigin,
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
        for (int row = -7; row < 8; row++)
        {
            int sourceY = Math.Clamp(integerY + row, 0, sourceHeight - 1);
            int sourceIndex = ((sourceOrigin.Y + sourceY) * sourceStride) + sourceOrigin.X + integerX - 7;
            ref byte sourceRow = ref Unsafe.Add(ref sourceBase, sourceIndex);
            ref ushort intermediateRow = ref intermediate[(row + 7) * WarpedTileSize];
            int phase = phaseX + (parameters.Beta * (row + 4));
            int column = 0;

            // Eight neighboring windows use different warped phases. Packing complete eight-tap windows into
            // descending SIMD widths preserves those independent coefficients while leaving no per-row buffers.
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
    private static void FilterWarpedHorizontal<TOperator>(
        ref ushort sourceBase,
        int sourceStride,
        Point sourceOrigin,
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
        for (int row = -7; row < 8; row++)
        {
            int sourceY = Math.Clamp(integerY + row, 0, sourceHeight - 1);
            int sourceIndex = ((sourceOrigin.Y + sourceY) * sourceStride) + sourceOrigin.X + integerX - 7;
            ref ushort sourceRow = ref Unsafe.Add(ref sourceBase, sourceIndex);
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
    private static Vector128<int> ConvolveWarpedVector256<TOperator>(ref byte source, int phase, int phaseStep)
        where TOperator : struct, IAv1WarpedPredictionOperator
        => TOperator.Convolve(
            Vector256.Create(LoadWarpedWindow(ref source), LoadWarpedWindow(ref Unsafe.Add(ref source, 1))),
            Vector256.Create(LoadWarpedCoefficients(phase), LoadWarpedCoefficients(phase + phaseStep)));

    /// <summary>
    /// Convolves one 8-bit source window.
    /// </summary>
    private static int ConvolveWarpedVector128<TOperator>(ref byte source, int phase)
        where TOperator : struct, IAv1WarpedPredictionOperator
        => TOperator.Convolve(LoadWarpedWindow(ref source), LoadWarpedCoefficients(phase));

    /// <summary>
    /// Convolves four adjacent high-bit-depth source windows with independent phases.
    /// </summary>
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
    private static Vector128<int> ConvolveWarpedVector256<TOperator>(ref ushort source, int sourceStride, int phase, int phaseStep)
        where TOperator : struct, IAv1WarpedPredictionOperator
        => TOperator.Convolve(
            Vector256.Create(LoadWarpedWindow(ref source, sourceStride), LoadWarpedWindow(ref Unsafe.Add(ref source, 1), sourceStride)),
            Vector256.Create(LoadWarpedCoefficients(phase), LoadWarpedCoefficients(phase + phaseStep)));

    /// <summary>
    /// Convolves one high-bit-depth source window.
    /// </summary>
    private static int ConvolveWarpedVector128<TOperator>(ref ushort source, int sourceStride, int phase)
        where TOperator : struct, IAv1WarpedPredictionOperator
        => TOperator.Convolve(LoadWarpedWindow(ref source, sourceStride), LoadWarpedCoefficients(phase));

    /// <summary>
    /// Loads eight adjacent unsigned byte samples as unsigned 16-bit lanes.
    /// </summary>
    private static Vector128<ushort> LoadWarpedWindow(ref byte source)
    {
        Vector128<byte> packed = Vector128.LoadUnsafe(ref source);
        return Vector128.Widen(packed).Lower;
    }

    /// <summary>
    /// Loads eight unsigned high-bit-depth samples with the supplied spacing.
    /// </summary>
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
    private static Vector128<short> LoadWarpedCoefficients(int phase)
        => Vector128.LoadUnsafe(ref GetWarpedFilterReference(phase));

    /// <summary>
    /// Applies the final 8-bit warped-prediction rounding and clipping.
    /// </summary>
    private static byte FinishWarpedByte(int sum, int bias, int round)
    {
        int value = RoundPowerOfTwoScalar(bias + sum, round) - (1 << 7) - (1 << 8);
        return (byte)Math.Clamp(value, byte.MinValue, byte.MaxValue);
    }

    /// <summary>
    /// Applies the compound-intermediate warped-prediction rounding.
    /// </summary>
    private static ushort FinishWarpedCompound(int sum, int bias)
        => (ushort)RoundPowerOfTwoScalar(bias + sum, Av1CompoundInterPredictor.CompoundRound1Bits);

    /// <summary>
    /// Applies the final high-bit-depth warped-prediction rounding and clipping.
    /// </summary>
    private static ushort FinishWarpedHighBitDepth(int sum, int bias, int round, int bitDepth, int maximum)
    {
        int value = RoundPowerOfTwoScalar(bias + sum, round) - (1 << (bitDepth - 1)) - (1 << bitDepth);
        return (ushort)Math.Clamp(value, 0, maximum);
    }

    /// <summary>
    /// Reconstructs one 8-bit warped reference without discarding the compound convolution precision.
    /// </summary>
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
    /// Reconstructs one high-bit-depth warped block through a closed convolution operator.
    /// </summary>
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

        // Twelve-bit prediction increases round0 by two so the biased horizontal intermediate remains representable
        // in sixteen bits. Reducing round1 by the same amount preserves the complete normative Q14 shift.
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
        phaseX += (-4 * parameters.Alpha) + (-4 * parameters.Beta);
        phaseY += (-4 * parameters.Gamma) + (-4 * parameters.Delta);

        // Shear parameters are quantized to 64-model-unit steps. Clearing the same low bits after the tile-center
        // projection keeps negative and positive phases on the exact filter-table grid used by the bitstream model.
        phaseX &= -1 << 6;
        phaseY &= -1 << 6;
    }

    /// <summary>
    /// Gets a reference to the first coefficient for one reduced warped-filter phase.
    /// </summary>
    private static ref short GetWarpedFilterReference(int phase)
    {
        int filterIndex = ((phase + (1 << (WarpedDifferencePrecisionBits - 1))) >> WarpedDifferencePrecisionBits) +
            WarpedPixelPrecisionShifts;

        return ref Unsafe.Add(ref MemoryMarshal.GetReference(WarpedFilter), filterIndex * FilterCoefficientCount);
    }

    /// <summary>
    /// Divides a nonnegative value by a power of two with nearest-integer rounding.
    /// </summary>
    private static int RoundPowerOfTwoScalar(int value, int bitCount)
        => (value + (1 << (bitCount - 1))) >> bitCount;
}
