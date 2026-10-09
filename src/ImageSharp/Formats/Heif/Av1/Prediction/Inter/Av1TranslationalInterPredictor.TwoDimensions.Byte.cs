// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Provides separable SIMD convolution for 8-bit single-reference prediction.
/// </content>
internal static partial class Av1TranslationalInterPredictor
{
    /// <summary>
    /// Filters an 8-bit block in sixteen-sample vectors through caller-owned signed intermediate rows.
    /// </summary>
    /// <param name="source">The complete padded reference plane.</param>
    /// <param name="sourceStride">The distance between reference rows in samples.</param>
    /// <param name="sourceOrigin">The index of the integer-position source sample within <paramref name="source"/>.</param>
    /// <param name="destination">The prediction block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The prediction width in samples.</param>
    /// <param name="height">The prediction height in samples.</param>
    /// <param name="horizontalCoefficients">The horizontal filter coefficients, starting at the first applied tap.</param>
    /// <param name="horizontalTapCount">The number of applied horizontal taps.</param>
    /// <param name="horizontalSourceOffset">The column offset from the integer-position sample to the first applied horizontal tap.</param>
    /// <param name="verticalCoefficients">The vertical filter coefficients, starting at the first applied tap.</param>
    /// <param name="verticalTapCount">The number of applied vertical taps.</param>
    /// <param name="verticalSourceOffset">The row offset from the integer-position sample to the first applied vertical tap.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="round0">The rounding shift of the horizontal pass. The vertical pass shifts by 14 minus this value.</param>
    /// <param name="intermediateRows">Signed intermediate storage of at least <see cref="GetScratchLength"/> elements.</param>
    /// <param name="initial">The start value of each accumulator lane. The vector type selects this overload.</param>
    private static void Filter2D(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int sourceOrigin,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height,
        ReadOnlySpan<short> horizontalCoefficients,
        int horizontalTapCount,
        int horizontalSourceOffset,
        ReadOnlySpan<short> verticalCoefficients,
        int verticalTapCount,
        int verticalSourceOffset,
        int bitDepth,
        int round0,
        Span<short> intermediateRows,
        Vector128<int> initial)
    {
        ref byte sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
        ref short intermediateBase = ref MemoryMarshal.GetReference(intermediateRows);
        ref short horizontalCoefficientBase = ref MemoryMarshal.GetReference(horizontalCoefficients);
        ref short verticalCoefficientBase = ref MemoryMarshal.GetReference(verticalCoefficients);
        int intermediateStride = Math.Max(width, MinimumScratchStride);
        int intermediateHeight = height + verticalTapCount - 1;

        // The horizontal bias of 2^(bitDepth + 6) keeps every intermediate value nonnegative and inside a signed 16-bit lane.
        Vector128<int> horizontalInitial = initial + Vector128.Create(1 << (bitDepth + FilterBits - 1));

        for (int row = 0; row < intermediateHeight; row++)
        {
            ref byte sourceRow = ref Unsafe.Add(ref sourceBase, ((row + verticalSourceOffset) * sourceStride) + horizontalSourceOffset);
            ref short intermediateRow = ref Unsafe.Add(ref intermediateBase, row * intermediateStride);
            int processedColumns = 0;

            if (width < Vector128<byte>.Count)
            {
                Convolve(
                    ref sourceRow,
                    1,
                    0,
                    ref horizontalCoefficientBase,
                    horizontalTapCount,
                    horizontalInitial,
                    out Vector128<int> result0,
                    out Vector128<int> result1,
                    out Vector128<int> result2,
                    out Vector128<int> result3);

                Av1NonDirectionalIntraPredictorBase.Narrow(RoundPowerOfTwo(result0, round0), RoundPowerOfTwo(result1, round0)).StoreUnsafe(ref intermediateRow);
                Av1NonDirectionalIntraPredictorBase.Narrow(RoundPowerOfTwo(result2, round0), RoundPowerOfTwo(result3, round0))
                    .StoreUnsafe(ref intermediateRow, (nuint)Vector128<short>.Count);

                continue;
            }

            nuint vectorCount = Numerics.Vector128Count<byte>(width - processedColumns);
            for (; vectorCount > 0; vectorCount--, processedColumns += Vector128<byte>.Count)
            {
                Convolve(
                    ref sourceRow,
                    1,
                    (nuint)processedColumns,
                    ref horizontalCoefficientBase,
                    horizontalTapCount,
                    horizontalInitial,
                    out Vector128<int> result0,
                    out Vector128<int> result1,
                    out Vector128<int> result2,
                    out Vector128<int> result3);

                Av1NonDirectionalIntraPredictorBase.Narrow(RoundPowerOfTwo(result0, round0), RoundPowerOfTwo(result1, round0))
                    .StoreUnsafe(ref intermediateRow, (nuint)processedColumns);

                Av1NonDirectionalIntraPredictorBase.Narrow(
                    RoundPowerOfTwo(result2, round0),
                    RoundPowerOfTwo(result3, round0)).StoreUnsafe(
                        ref intermediateRow,
                        (nuint)(processedColumns + Vector128<short>.Count));
            }

            FilterHorizontalTail(
                ref sourceRow,
                ref intermediateRow,
                processedColumns,
                width,
                ref horizontalCoefficientBase,
                horizontalTapCount,
                bitDepth,
                round0);
        }

        // The vertical pass adds a bias of 2^offsetBits. After the round1 shift, roundOffset removes it and the horizontal bias.
        int round1 = (2 * FilterBits) - round0;
        int offsetBits = bitDepth + (2 * FilterBits) - round0;
        Vector128<int> verticalInitial = initial + Vector128.Create(1 << offsetBits);
        Vector128<int> roundOffset = Vector128.Create((1 << (offsetBits - round1)) + (1 << (offsetBits - round1 - 1)));

        for (int row = 0; row < height; row++)
        {
            ref short intermediateRow = ref Unsafe.Add(ref intermediateBase, row * intermediateStride);
            ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int processedColumns = 0;

            if (width < Vector128<byte>.Count)
            {
                Convolve(
                    ref intermediateRow,
                    intermediateStride,
                    0,
                    ref verticalCoefficientBase,
                    verticalTapCount,
                    verticalInitial,
                    out Vector128<int> result0,
                    out Vector128<int> result1);

                Convolve(
                    ref Unsafe.Add(ref intermediateRow, Vector128<short>.Count),
                    intermediateStride,
                    0,
                    ref verticalCoefficientBase,
                    verticalTapCount,
                    verticalInitial,
                    out Vector128<int> result2,
                    out Vector128<int> result3);

                result0 = RoundPowerOfTwo(result0, round1) - roundOffset;
                result1 = RoundPowerOfTwo(result1, round1) - roundOffset;
                result2 = RoundPowerOfTwo(result2, round1) - roundOffset;
                result3 = RoundPowerOfTwo(result3, round1) - roundOffset;
                StorePartial(PackBytes(result0, result1, result2, result3), ref destinationRow, width);
                continue;
            }

            nuint vectorCount = Numerics.Vector128Count<byte>(width - processedColumns);
            for (; vectorCount > 0; vectorCount--, processedColumns += Vector128<byte>.Count)
            {
                Convolve(
                    ref intermediateRow,
                    intermediateStride,
                    (nuint)processedColumns,
                    ref verticalCoefficientBase,
                    verticalTapCount,
                    verticalInitial,
                    out Vector128<int> result0,
                    out Vector128<int> result1);

                Convolve(
                    ref intermediateRow,
                    intermediateStride,
                    (nuint)(processedColumns + Vector128<short>.Count),
                    ref verticalCoefficientBase,
                    verticalTapCount,
                    verticalInitial,
                    out Vector128<int> result2,
                    out Vector128<int> result3);

                result0 = RoundPowerOfTwo(result0, round1) - roundOffset;
                result1 = RoundPowerOfTwo(result1, round1) - roundOffset;
                result2 = RoundPowerOfTwo(result2, round1) - roundOffset;
                result3 = RoundPowerOfTwo(result3, round1) - roundOffset;
                PackBytes(result0, result1, result2, result3).StoreUnsafe(ref destinationRow, (nuint)processedColumns);
            }

            FilterVerticalTail(
                ref intermediateRow,
                ref destinationRow,
                processedColumns,
                width,
                intermediateStride,
                ref verticalCoefficientBase,
                verticalTapCount,
                bitDepth,
                round0);
        }
    }

    /// <summary>
    /// Filters an 8-bit block in thirty-two-sample vectors through caller-owned signed intermediate rows.
    /// </summary>
    /// <param name="source">The complete padded reference plane.</param>
    /// <param name="sourceStride">The distance between reference rows in samples.</param>
    /// <param name="sourceOrigin">The index of the integer-position source sample within <paramref name="source"/>.</param>
    /// <param name="destination">The prediction block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The prediction width in samples.</param>
    /// <param name="height">The prediction height in samples.</param>
    /// <param name="horizontalCoefficients">The horizontal filter coefficients, starting at the first applied tap.</param>
    /// <param name="horizontalTapCount">The number of applied horizontal taps.</param>
    /// <param name="horizontalSourceOffset">The column offset from the integer-position sample to the first applied horizontal tap.</param>
    /// <param name="verticalCoefficients">The vertical filter coefficients, starting at the first applied tap.</param>
    /// <param name="verticalTapCount">The number of applied vertical taps.</param>
    /// <param name="verticalSourceOffset">The row offset from the integer-position sample to the first applied vertical tap.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="round0">The rounding shift of the horizontal pass. The vertical pass shifts by 14 minus this value.</param>
    /// <param name="intermediateRows">Signed intermediate storage of at least <see cref="GetScratchLength"/> elements.</param>
    /// <param name="initial">The start value of each accumulator lane. The vector type selects this overload.</param>
    private static void Filter2D(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int sourceOrigin,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height,
        ReadOnlySpan<short> horizontalCoefficients,
        int horizontalTapCount,
        int horizontalSourceOffset,
        ReadOnlySpan<short> verticalCoefficients,
        int verticalTapCount,
        int verticalSourceOffset,
        int bitDepth,
        int round0,
        Span<short> intermediateRows,
        Vector256<int> initial)
    {
        ref byte sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
        ref short intermediateBase = ref MemoryMarshal.GetReference(intermediateRows);
        ref short horizontalCoefficientBase = ref MemoryMarshal.GetReference(horizontalCoefficients);
        ref short verticalCoefficientBase = ref MemoryMarshal.GetReference(verticalCoefficients);
        int intermediateStride = Math.Max(width, MinimumScratchStride);
        int intermediateHeight = height + verticalTapCount - 1;
        Vector256<int> horizontalInitial = initial + Vector256.Create(1 << (bitDepth + FilterBits - 1));
        int vectorEnd = (int)(Numerics.Vector256Count<byte>(width) * (nuint)Vector256<byte>.Count);

        for (int row = 0; row < intermediateHeight; row++)
        {
            ref byte sourceRow = ref Unsafe.Add(ref sourceBase, ((row + verticalSourceOffset) * sourceStride) + horizontalSourceOffset);
            ref short intermediateRow = ref Unsafe.Add(ref intermediateBase, row * intermediateStride);
            int processedColumns = 0;

            for (; processedColumns < vectorEnd; processedColumns += Vector256<byte>.Count)
            {
                Convolve(
                    ref sourceRow,
                    1,
                    (nuint)processedColumns,
                    ref horizontalCoefficientBase,
                    horizontalTapCount,
                    horizontalInitial,
                    out Vector256<int> result0,
                    out Vector256<int> result1,
                    out Vector256<int> result2,
                    out Vector256<int> result3);

                Av1NonDirectionalIntraPredictorBase.Narrow(RoundPowerOfTwo(result0, round0), RoundPowerOfTwo(result1, round0))
                    .StoreUnsafe(ref intermediateRow, (nuint)processedColumns);

                Av1NonDirectionalIntraPredictorBase.Narrow(
                    RoundPowerOfTwo(result2, round0),
                    RoundPowerOfTwo(result3, round0)).StoreUnsafe(
                        ref intermediateRow,
                        (nuint)(processedColumns + Vector256<short>.Count));
            }

            FilterHorizontalTail(
                ref sourceRow,
                ref intermediateRow,
                processedColumns,
                width,
                ref horizontalCoefficientBase,
                horizontalTapCount,
                bitDepth,
                round0);
        }

        int round1 = (2 * FilterBits) - round0;
        int offsetBits = bitDepth + (2 * FilterBits) - round0;
        Vector256<int> verticalInitial = initial + Vector256.Create(1 << offsetBits);
        Vector256<int> roundOffset = Vector256.Create((1 << (offsetBits - round1)) + (1 << (offsetBits - round1 - 1)));

        for (int row = 0; row < height; row++)
        {
            ref short intermediateRow = ref Unsafe.Add(ref intermediateBase, row * intermediateStride);
            ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int processedColumns = 0;

            for (; processedColumns < vectorEnd; processedColumns += Vector256<byte>.Count)
            {
                Convolve(
                    ref intermediateRow,
                    intermediateStride,
                    (nuint)processedColumns,
                    ref verticalCoefficientBase,
                    verticalTapCount,
                    verticalInitial,
                    out Vector256<int> result0,
                    out Vector256<int> result1);

                Convolve(
                    ref intermediateRow,
                    intermediateStride,
                    (nuint)(processedColumns + Vector256<short>.Count),
                    ref verticalCoefficientBase,
                    verticalTapCount,
                    verticalInitial,
                    out Vector256<int> result2,
                    out Vector256<int> result3);

                result0 = RoundPowerOfTwo(result0, round1) - roundOffset;
                result1 = RoundPowerOfTwo(result1, round1) - roundOffset;
                result2 = RoundPowerOfTwo(result2, round1) - roundOffset;
                result3 = RoundPowerOfTwo(result3, round1) - roundOffset;
                PackBytes(result0, result1, result2, result3).StoreUnsafe(ref destinationRow, (nuint)processedColumns);
            }

            FilterVerticalTail(
                ref intermediateRow,
                ref destinationRow,
                processedColumns,
                width,
                intermediateStride,
                ref verticalCoefficientBase,
                verticalTapCount,
                bitDepth,
                round0);
        }
    }

    /// <summary>
    /// Filters an 8-bit block in sixty-four-sample vectors through caller-owned signed intermediate rows.
    /// </summary>
    /// <param name="source">The complete padded reference plane.</param>
    /// <param name="sourceStride">The distance between reference rows in samples.</param>
    /// <param name="sourceOrigin">The index of the integer-position source sample within <paramref name="source"/>.</param>
    /// <param name="destination">The prediction block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The prediction width in samples.</param>
    /// <param name="height">The prediction height in samples.</param>
    /// <param name="horizontalCoefficients">The horizontal filter coefficients, starting at the first applied tap.</param>
    /// <param name="horizontalTapCount">The number of applied horizontal taps.</param>
    /// <param name="horizontalSourceOffset">The column offset from the integer-position sample to the first applied horizontal tap.</param>
    /// <param name="verticalCoefficients">The vertical filter coefficients, starting at the first applied tap.</param>
    /// <param name="verticalTapCount">The number of applied vertical taps.</param>
    /// <param name="verticalSourceOffset">The row offset from the integer-position sample to the first applied vertical tap.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="round0">The rounding shift of the horizontal pass. The vertical pass shifts by 14 minus this value.</param>
    /// <param name="intermediateRows">Signed intermediate storage of at least <see cref="GetScratchLength"/> elements.</param>
    /// <param name="initial">The start value of each accumulator lane. The vector type selects this overload.</param>
    private static void Filter2D(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int sourceOrigin,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height,
        ReadOnlySpan<short> horizontalCoefficients,
        int horizontalTapCount,
        int horizontalSourceOffset,
        ReadOnlySpan<short> verticalCoefficients,
        int verticalTapCount,
        int verticalSourceOffset,
        int bitDepth,
        int round0,
        Span<short> intermediateRows,
        Vector512<int> initial)
    {
        ref byte sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
        ref short intermediateBase = ref MemoryMarshal.GetReference(intermediateRows);
        ref short horizontalCoefficientBase = ref MemoryMarshal.GetReference(horizontalCoefficients);
        ref short verticalCoefficientBase = ref MemoryMarshal.GetReference(verticalCoefficients);
        int intermediateStride = Math.Max(width, MinimumScratchStride);
        int intermediateHeight = height + verticalTapCount - 1;
        Vector512<int> horizontalInitial = initial + Vector512.Create(1 << (bitDepth + FilterBits - 1));
        int vectorEnd = (int)(Numerics.Vector512Count<byte>(width) * (nuint)Vector512<byte>.Count);

        for (int row = 0; row < intermediateHeight; row++)
        {
            ref byte sourceRow = ref Unsafe.Add(ref sourceBase, ((row + verticalSourceOffset) * sourceStride) + horizontalSourceOffset);
            ref short intermediateRow = ref Unsafe.Add(ref intermediateBase, row * intermediateStride);
            int processedColumns = 0;

            for (; processedColumns < vectorEnd; processedColumns += Vector512<byte>.Count)
            {
                Convolve(
                    ref sourceRow,
                    1,
                    (nuint)processedColumns,
                    ref horizontalCoefficientBase,
                    horizontalTapCount,
                    horizontalInitial,
                    out Vector512<int> result0,
                    out Vector512<int> result1,
                    out Vector512<int> result2,
                    out Vector512<int> result3);

                Av1NonDirectionalIntraPredictorBase.Narrow(RoundPowerOfTwo(result0, round0), RoundPowerOfTwo(result1, round0))
                    .StoreUnsafe(ref intermediateRow, (nuint)processedColumns);

                Av1NonDirectionalIntraPredictorBase.Narrow(
                    RoundPowerOfTwo(result2, round0),
                    RoundPowerOfTwo(result3, round0)).StoreUnsafe(
                        ref intermediateRow,
                        (nuint)(processedColumns + Vector512<short>.Count));
            }

            FilterHorizontalTail(
                ref sourceRow,
                ref intermediateRow,
                processedColumns,
                width,
                ref horizontalCoefficientBase,
                horizontalTapCount,
                bitDepth,
                round0);
        }

        int round1 = (2 * FilterBits) - round0;
        int offsetBits = bitDepth + (2 * FilterBits) - round0;
        Vector512<int> verticalInitial = initial + Vector512.Create(1 << offsetBits);
        Vector512<int> roundOffset = Vector512.Create((1 << (offsetBits - round1)) + (1 << (offsetBits - round1 - 1)));

        for (int row = 0; row < height; row++)
        {
            ref short intermediateRow = ref Unsafe.Add(ref intermediateBase, row * intermediateStride);
            ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);
            int processedColumns = 0;

            for (; processedColumns < vectorEnd; processedColumns += Vector512<byte>.Count)
            {
                Convolve(
                    ref intermediateRow,
                    intermediateStride,
                    (nuint)processedColumns,
                    ref verticalCoefficientBase,
                    verticalTapCount,
                    verticalInitial,
                    out Vector512<int> result0,
                    out Vector512<int> result1);

                Convolve(
                    ref intermediateRow,
                    intermediateStride,
                    (nuint)(processedColumns + Vector512<short>.Count),
                    ref verticalCoefficientBase,
                    verticalTapCount,
                    verticalInitial,
                    out Vector512<int> result2,
                    out Vector512<int> result3);

                result0 = RoundPowerOfTwo(result0, round1) - roundOffset;
                result1 = RoundPowerOfTwo(result1, round1) - roundOffset;
                result2 = RoundPowerOfTwo(result2, round1) - roundOffset;
                result3 = RoundPowerOfTwo(result3, round1) - roundOffset;
                PackBytes(result0, result1, result2, result3).StoreUnsafe(ref destinationRow, (nuint)processedColumns);
            }

            FilterVerticalTail(
                ref intermediateRow,
                ref destinationRow,
                processedColumns,
                width,
                intermediateStride,
                ref verticalCoefficientBase,
                verticalTapCount,
                bitDepth,
                round0);
        }
    }

    /// <summary>
    /// Finishes an 8-bit horizontal intermediate row after its selected vector width.
    /// </summary>
    /// <param name="source">The sample that the first horizontal tap reads for column zero.</param>
    /// <param name="intermediateRows">The first sample of the intermediate row.</param>
    /// <param name="firstColumn">The first column that the vector loop did not write.</param>
    /// <param name="width">The row width in samples.</param>
    /// <param name="coefficients">The first applied horizontal coefficient.</param>
    /// <param name="tapCount">The number of applied horizontal taps.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="round0">The rounding shift of the horizontal pass.</param>
    private static void FilterHorizontalTail(
        ref byte source,
        ref short intermediateRows,
        int firstColumn,
        int width,
        ref short coefficients,
        int tapCount,
        int bitDepth,
        int round0)
    {
        int horizontalBias = 1 << (bitDepth + FilterBits - 1);
        for (int column = firstColumn; column < width; column++)
        {
            int sum = horizontalBias + ConvolveScalar(ref Unsafe.Add(ref source, column), 1, ref coefficients, tapCount);
            Unsafe.Add(ref intermediateRows, column) = (short)RoundPowerOfTwo(sum, round0);
        }
    }

    /// <summary>
    /// Finishes an 8-bit vertical output row after its selected vector width.
    /// </summary>
    /// <param name="intermediateRows">The intermediate sample that the first vertical tap reads for column zero.</param>
    /// <param name="destination">The first sample of the destination row.</param>
    /// <param name="firstColumn">The first column that the vector loop did not write.</param>
    /// <param name="width">The row width in samples.</param>
    /// <param name="intermediateStride">The distance between intermediate rows in samples.</param>
    /// <param name="coefficients">The first applied vertical coefficient.</param>
    /// <param name="tapCount">The number of applied vertical taps.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="round0">The rounding shift of the horizontal pass. The vertical pass shifts by 14 minus this value.</param>
    private static void FilterVerticalTail(
        ref short intermediateRows,
        ref byte destination,
        int firstColumn,
        int width,
        int intermediateStride,
        ref short coefficients,
        int tapCount,
        int bitDepth,
        int round0)
    {
        int round1 = (2 * FilterBits) - round0;
        int offsetBits = bitDepth + (2 * FilterBits) - round0;
        int verticalBias = 1 << offsetBits;
        int roundOffset = (1 << (offsetBits - round1)) + (1 << (offsetBits - round1 - 1));

        for (int column = firstColumn; column < width; column++)
        {
            int sum = verticalBias + ConvolveScalar(ref Unsafe.Add(ref intermediateRows, column), intermediateStride, ref coefficients, tapCount);
            int result = RoundPowerOfTwo(sum, round1) - roundOffset;
            Unsafe.Add(ref destination, column) = (byte)Math.Clamp(result, byte.MinValue, byte.MaxValue);
        }
    }

    /// <summary>
    /// Applies separable two-dimensional filtering to an 8-bit block without explicit hardware intrinsics.
    /// </summary>
    /// <param name="source">The complete padded reference plane.</param>
    /// <param name="sourceStride">The distance between reference rows in samples.</param>
    /// <param name="sourceOrigin">The index of the integer-position source sample within <paramref name="source"/>.</param>
    /// <param name="destination">The prediction block destination.</param>
    /// <param name="destinationStride">The distance between destination rows in samples.</param>
    /// <param name="width">The prediction width in samples.</param>
    /// <param name="height">The prediction height in samples.</param>
    /// <param name="horizontalCoefficients">The horizontal filter coefficients, starting at the first applied tap.</param>
    /// <param name="horizontalTapCount">The number of applied horizontal taps.</param>
    /// <param name="horizontalSourceOffset">The column offset from the integer-position sample to the first applied horizontal tap.</param>
    /// <param name="verticalCoefficients">The vertical filter coefficients, starting at the first applied tap.</param>
    /// <param name="verticalTapCount">The number of applied vertical taps.</param>
    /// <param name="verticalSourceOffset">The row offset from the integer-position sample to the first applied vertical tap.</param>
    /// <param name="bitDepth">The sample bit depth.</param>
    /// <param name="round0">The rounding shift of the horizontal pass. The vertical pass shifts by 14 minus this value.</param>
    /// <param name="intermediateRows">Signed intermediate storage of at least <see cref="GetScratchLength"/> elements.</param>
    private static void Filter2DScalar(
        ReadOnlySpan<byte> source,
        int sourceStride,
        int sourceOrigin,
        Span<byte> destination,
        int destinationStride,
        int width,
        int height,
        ReadOnlySpan<short> horizontalCoefficients,
        int horizontalTapCount,
        int horizontalSourceOffset,
        ReadOnlySpan<short> verticalCoefficients,
        int verticalTapCount,
        int verticalSourceOffset,
        int bitDepth,
        int round0,
        Span<short> intermediateRows)
    {
        ref byte sourceBase = ref Unsafe.Add(ref MemoryMarshal.GetReference(source), sourceOrigin);
        ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
        ref short intermediateBase = ref MemoryMarshal.GetReference(intermediateRows);
        ref short horizontalCoefficientBase = ref MemoryMarshal.GetReference(horizontalCoefficients);
        ref short verticalCoefficientBase = ref MemoryMarshal.GetReference(verticalCoefficients);
        int intermediateStride = Math.Max(width, MinimumScratchStride);
        int intermediateHeight = height + verticalTapCount - 1;
        int horizontalBias = 1 << (bitDepth + FilterBits - 1);

        // The first intermediate row is the row of the first applied vertical tap, so the horizontal pass starts at or above the source origin.
        // It writes one row for each vertical tap position that the vertical pass reads.
        for (int row = 0; row < intermediateHeight; row++)
        {
            ref byte sourceRow = ref Unsafe.Add(ref sourceBase, ((row + verticalSourceOffset) * sourceStride) + horizontalSourceOffset);
            ref short intermediateRow = ref Unsafe.Add(ref intermediateBase, row * intermediateStride);

            for (int column = 0; column < width; column++)
            {
                int sum = horizontalBias + ConvolveScalar(ref Unsafe.Add(ref sourceRow, column), 1, ref horizontalCoefficientBase, horizontalTapCount);
                Unsafe.Add(ref intermediateRow, column) = (short)RoundPowerOfTwo(sum, round0);
            }
        }

        int round1 = (2 * FilterBits) - round0;
        int offsetBits = bitDepth + (2 * FilterBits) - round0;
        int verticalBias = 1 << offsetBits;
        int roundOffset = (1 << (offsetBits - round1)) + (1 << (offsetBits - round1 - 1));

        // The horizontal bias of 2^(bitDepth + 6) keeps every intermediate value nonnegative and inside a signed 16-bit lane.
        // The vertical pass adds a second bias of 2^offsetBits. After the round1 shift, roundOffset removes both biases.
        // This gives exactly the normative single-reference rounding.
        for (int row = 0; row < height; row++)
        {
            ref short intermediateRow = ref Unsafe.Add(ref intermediateBase, row * intermediateStride);
            ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

            for (int column = 0; column < width; column++)
            {
                int sum = verticalBias + ConvolveScalar(
                    ref Unsafe.Add(ref intermediateRow, column),
                    intermediateStride,
                    ref verticalCoefficientBase,
                    verticalTapCount);

                int result = RoundPowerOfTwo(sum, round1) - roundOffset;
                Unsafe.Add(ref destinationRow, column) = (byte)Math.Clamp(result, byte.MinValue, byte.MaxValue);
            }
        }
    }
}
