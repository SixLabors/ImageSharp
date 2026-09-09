// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Provides the SIMD data-layout operations shared by AV1 two-dimensional transforms.
/// </summary>
/// <remarks>
/// The one-dimensional operators expect one transform position per vector and one independent axis per lane. These
/// routines transpose rectangular sample tiles into that structure, then transpose the completed axes back to raster
/// order. Every shuffle is consequently an index-bit exchange between row and column coordinates; it does not alter
/// the signed fixed-point sample representation.
/// </remarks>
internal static class Av1Transform2dOperations
{
    /// <summary>
    /// Gets the row order produced by the final AVX-512 16-by-16 transpose concatenation.
    /// </summary>
    private static ReadOnlySpan<byte> Vector512TransposeStoreOrder => [0, 2, 1, 3, 4, 6, 5, 7, 8, 10, 9, 11, 12, 14, 13, 15];

    /// <summary>
    /// Gets the row order produced by the final sixteen-bit 16-by-16 transpose concatenation.
    /// </summary>
    private static ReadOnlySpan<byte> Int16TransposeStoreOrder => [0, 4, 2, 6, 1, 5, 3, 7, 8, 12, 10, 14, 9, 13, 11, 15];

    /// <summary>
    /// Loads four signed sixteen-bit values and widens them to four signed thirty-two-bit lanes.
    /// </summary>
    /// <param name="source">The first source value.</param>
    /// <returns>The four widened values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<int> Load4Int16(ref short source)
    {
        ulong packed = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<short, byte>(ref source));
        return Vector128.WidenLower(Vector128.CreateScalarUnsafe(packed).AsInt16());
    }

    /// <summary>
    /// Loads eight signed sixteen-bit values and widens them to eight signed thirty-two-bit lanes.
    /// </summary>
    /// <param name="source">The first source value.</param>
    /// <returns>The eight widened values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<int> Load8Int16(ref short source)
        => Vector256_.Widen(Vector128.LoadUnsafe(ref source));

    /// <summary>
    /// Loads sixteen signed sixteen-bit values and widens them to sixteen signed thirty-two-bit lanes.
    /// </summary>
    /// <param name="source">The first source value.</param>
    /// <returns>The sixteen widened values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<int> Load16Int16(ref short source)
    {
        (Vector256<int> lower, Vector256<int> upper) = Vector256.Widen(Vector256.LoadUnsafe(ref source));

        return Vector512.Create(lower, upper);
    }

    /// <summary>
    /// Applies a signed AV1 pipeline shift to four values in parallel.
    /// </summary>
    /// <param name="value">The values to shift.</param>
    /// <param name="bit">A positive rounded-right shift or a negative exact-left shift.</param>
    /// <returns>The shifted values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<int> RoundShift(Vector128<int> value, int bit)
    {
        if (bit > 0)
        {
            return (value + Vector128.Create(1 << (bit - 1))) >> bit;
        }

        return bit < 0 ? value << -bit : value;
    }

    /// <summary>
    /// Applies a signed AV1 pipeline shift to eight values in parallel.
    /// </summary>
    /// <param name="value">The values to shift.</param>
    /// <param name="bit">A positive rounded-right shift or a negative exact-left shift.</param>
    /// <returns>The shifted values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<int> RoundShift(Vector256<int> value, int bit)
    {
        if (bit > 0)
        {
            return (value + Vector256.Create(1 << (bit - 1))) >> bit;
        }

        return bit < 0 ? value << -bit : value;
    }

    /// <summary>
    /// Applies a signed AV1 pipeline shift to sixteen values in parallel.
    /// </summary>
    /// <param name="value">The values to shift.</param>
    /// <param name="bit">A positive rounded-right shift or a negative exact-left shift.</param>
    /// <returns>The shifted values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<int> RoundShift(Vector512<int> value, int bit)
    {
        if (bit > 0)
        {
            return (value + Vector512.Create(1 << (bit - 1))) >> bit;
        }

        return bit < 0 ? value << -bit : value;
    }

    /// <summary>
    /// Applies a signed AV1 pipeline shift to eight signed sixteen-bit values in parallel.
    /// </summary>
    /// <param name="value">The values to shift.</param>
    /// <param name="bit">A positive rounded-right shift or a negative exact-left shift.</param>
    /// <returns>The shifted values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<short> RoundShift(Vector128<short> value, int bit)
    {
        if (bit > 0)
        {
            return (value + Vector128.Create((short)(1 << (bit - 1)))) >> bit;
        }

        return bit < 0 ? value << -bit : value;
    }

    /// <summary>
    /// Applies a signed AV1 pipeline shift to sixteen signed sixteen-bit values in parallel.
    /// </summary>
    /// <param name="value">The values to shift.</param>
    /// <param name="bit">A positive rounded-right shift or a negative exact-left shift.</param>
    /// <returns>The shifted values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<short> RoundShift(Vector256<short> value, int bit)
    {
        if (bit > 0)
        {
            // Conformant low-bit-depth stage ranges leave room for the rounding bias, so this intentionally uses the
            // wrapping add used by the reference decoder rather than changing the normative result with a saturating instruction.
            return (value + Vector256.Create((short)(1 << (bit - 1)))) >> bit;
        }

        return bit < 0 ? value << -bit : value;
    }

    /// <summary>
    /// Applies a signed AV1 pipeline shift to thirty-two signed sixteen-bit values in parallel.
    /// </summary>
    /// <param name="value">The values to shift.</param>
    /// <param name="bit">A positive rounded-right shift or a negative exact-left shift.</param>
    /// <returns>The shifted values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<short> RoundShift(Vector512<short> value, int bit)
    {
        if (bit > 0)
        {
            return (value + Vector512.Create((short)(1 << (bit - 1)))) >> bit;
        }

        return bit < 0 ? value << -bit : value;
    }

    /// <summary>
    /// Transposes one 4-by-4 tile of signed sixteen-bit values and applies the configured pipeline operations.
    /// </summary>
    /// <param name="source">The first value of the source tile.</param>
    /// <param name="sourceStride">The number of signed sixteen-bit values between source rows.</param>
    /// <param name="destination">The first value of the destination tile.</param>
    /// <param name="destinationStride">The number of signed sixteen-bit values between destination rows.</param>
    /// <param name="roundShift">The signed AV1 pipeline shift applied before transposition.</param>
    /// <param name="normalizeRectangle">Whether to apply the AV1 square-root-of-two rectangular normalization.</param>
    public static void Transpose4x4Int16(
        ref short source,
        int sourceStride,
        ref short destination,
        int destinationStride,
        int roundShift,
        bool normalizeRectangle)
    {
        Vector128<short> row0 = Load4Short(ref source);
        Vector128<short> row1 = Load4Short(ref Unsafe.Add(ref source, sourceStride));
        Vector128<short> row2 = Load4Short(ref Unsafe.Add(ref source, 2 * sourceStride));
        Vector128<short> row3 = Load4Short(ref Unsafe.Add(ref source, 3 * sourceStride));

        row0 = Finish(row0, roundShift, normalizeRectangle);
        row1 = Finish(row1, roundShift, normalizeRectangle);
        row2 = Finish(row2, roundShift, normalizeRectangle);
        row3 = Finish(row3, roundShift, normalizeRectangle);

        // Only the lower four lanes belong to the tile. Interleaving at Int16 and Int32 granularity exchanges the
        // two row-index bits with the corresponding column-index bits without touching adjacent padded storage.
        Vector128<short> pair0 = Vector128_.UnpackLow(row0, row1);
        Vector128<short> pair1 = Vector128_.UnpackLow(row2, row3);
        Vector128<int> columns01 = Vector128_.UnpackLow(pair0.AsInt32(), pair1.AsInt32());
        Vector128<int> columns23 = Vector128_.UnpackHigh(pair0.AsInt32(), pair1.AsInt32());

        Store4Int16(columns01.AsUInt64().ToScalar(), ref destination);
        Store4Int16(columns01.AsUInt64().GetElement(1), ref Unsafe.Add(ref destination, destinationStride));
        Store4Int16(columns23.AsUInt64().ToScalar(), ref Unsafe.Add(ref destination, 2 * destinationStride));
        Store4Int16(columns23.AsUInt64().GetElement(1), ref Unsafe.Add(ref destination, 3 * destinationStride));
    }

    /// <summary>
    /// Transposes one 8-by-8 tile of signed sixteen-bit values and applies the configured pipeline operations.
    /// </summary>
    /// <param name="source">The first value of the source tile.</param>
    /// <param name="sourceStride">The number of signed sixteen-bit values between source rows.</param>
    /// <param name="destination">The first value of the destination tile.</param>
    /// <param name="destinationStride">The number of signed sixteen-bit values between destination rows.</param>
    /// <param name="roundShift">The signed AV1 pipeline shift applied before transposition.</param>
    /// <param name="normalizeRectangle">Whether to apply the AV1 square-root-of-two rectangular normalization.</param>
    public static void Transpose8x8Int16(
        ref short source,
        int sourceStride,
        ref short destination,
        int destinationStride,
        int roundShift,
        bool normalizeRectangle)
    {
        Vector128<short> row0 = Finish(Vector128.LoadUnsafe(ref source), roundShift, normalizeRectangle);
        Vector128<short> row1 = Finish(Vector128.LoadUnsafe(ref source, (nuint)sourceStride), roundShift, normalizeRectangle);
        Vector128<short> row2 = Finish(Vector128.LoadUnsafe(ref source, (nuint)(2 * sourceStride)), roundShift, normalizeRectangle);
        Vector128<short> row3 = Finish(Vector128.LoadUnsafe(ref source, (nuint)(3 * sourceStride)), roundShift, normalizeRectangle);
        Vector128<short> row4 = Finish(Vector128.LoadUnsafe(ref source, (nuint)(4 * sourceStride)), roundShift, normalizeRectangle);
        Vector128<short> row5 = Finish(Vector128.LoadUnsafe(ref source, (nuint)(5 * sourceStride)), roundShift, normalizeRectangle);
        Vector128<short> row6 = Finish(Vector128.LoadUnsafe(ref source, (nuint)(6 * sourceStride)), roundShift, normalizeRectangle);
        Vector128<short> row7 = Finish(Vector128.LoadUnsafe(ref source, (nuint)(7 * sourceStride)), roundShift, normalizeRectangle);

        Vector128<short> pair0 = Vector128_.UnpackLow(row0, row1);
        Vector128<short> pair1 = Vector128_.UnpackHigh(row0, row1);
        Vector128<short> pair2 = Vector128_.UnpackLow(row2, row3);
        Vector128<short> pair3 = Vector128_.UnpackHigh(row2, row3);
        Vector128<short> pair4 = Vector128_.UnpackLow(row4, row5);
        Vector128<short> pair5 = Vector128_.UnpackHigh(row4, row5);
        Vector128<short> pair6 = Vector128_.UnpackLow(row6, row7);
        Vector128<short> pair7 = Vector128_.UnpackHigh(row6, row7);
        Vector128<int> quad0 = Vector128_.UnpackLow(pair0.AsInt32(), pair2.AsInt32());
        Vector128<int> quad1 = Vector128_.UnpackHigh(pair0.AsInt32(), pair2.AsInt32());
        Vector128<int> quad2 = Vector128_.UnpackLow(pair1.AsInt32(), pair3.AsInt32());
        Vector128<int> quad3 = Vector128_.UnpackHigh(pair1.AsInt32(), pair3.AsInt32());
        Vector128<int> quad4 = Vector128_.UnpackLow(pair4.AsInt32(), pair6.AsInt32());
        Vector128<int> quad5 = Vector128_.UnpackHigh(pair4.AsInt32(), pair6.AsInt32());
        Vector128<int> quad6 = Vector128_.UnpackLow(pair5.AsInt32(), pair7.AsInt32());
        Vector128<int> quad7 = Vector128_.UnpackHigh(pair5.AsInt32(), pair7.AsInt32());

        Vector128_.UnpackLow(quad0.AsInt64(), quad4.AsInt64()).AsInt16().StoreUnsafe(ref destination);
        Vector128_.UnpackHigh(quad0.AsInt64(), quad4.AsInt64()).AsInt16().StoreUnsafe(ref destination, (nuint)destinationStride);
        Vector128_.UnpackLow(quad1.AsInt64(), quad5.AsInt64()).AsInt16().StoreUnsafe(ref destination, (nuint)(2 * destinationStride));
        Vector128_.UnpackHigh(quad1.AsInt64(), quad5.AsInt64()).AsInt16().StoreUnsafe(ref destination, (nuint)(3 * destinationStride));
        Vector128_.UnpackLow(quad2.AsInt64(), quad6.AsInt64()).AsInt16().StoreUnsafe(ref destination, (nuint)(4 * destinationStride));
        Vector128_.UnpackHigh(quad2.AsInt64(), quad6.AsInt64()).AsInt16().StoreUnsafe(ref destination, (nuint)(5 * destinationStride));
        Vector128_.UnpackLow(quad3.AsInt64(), quad7.AsInt64()).AsInt16().StoreUnsafe(ref destination, (nuint)(6 * destinationStride));
        Vector128_.UnpackHigh(quad3.AsInt64(), quad7.AsInt64()).AsInt16().StoreUnsafe(ref destination, (nuint)(7 * destinationStride));
    }

    /// <summary>
    /// Transposes one 16-by-16 tile of signed sixteen-bit values and applies the configured pipeline shift.
    /// </summary>
    /// <param name="source">The first value of the source tile.</param>
    /// <param name="sourceStride">The number of signed sixteen-bit values between source rows.</param>
    /// <param name="destination">The first value of the destination tile.</param>
    /// <param name="destinationStride">The number of signed sixteen-bit values between destination rows.</param>
    /// <param name="scratch">The reusable storage for the widening transpose stages.</param>
    /// <param name="roundShift">The signed AV1 pipeline shift applied before transposition.</param>
    /// <param name="normalizeRectangle">Whether to apply the AV1 square-root-of-two rectangular normalization.</param>
    public static void Transpose16x16Int16(
        ref short source,
        int sourceStride,
        ref short destination,
        int destinationStride,
        Span<long> scratch,
        int roundShift,
        bool normalizeRectangle)
    {
        ref long scratch64 = ref MemoryMarshal.GetReference(scratch);
        ref int scratch32 = ref Unsafe.As<long, int>(ref scratch64);

        // Pairing adjacent rows widens groups of two Int16 values into Int32 storage. The widening is a bitwise
        // reinterpretation: it preserves all sixteen source bits while progressively exchanging row and column bits.
        for (int row = 0; row < 16; row += 2)
        {
            Vector256<short> even = Vector256.LoadUnsafe(ref source, (nuint)(row * sourceStride));
            Vector256<short> odd = Vector256.LoadUnsafe(ref source, (nuint)((row + 1) * sourceStride));
            even = RoundShift(even, roundShift);
            odd = RoundShift(odd, roundShift);

            if (normalizeRectangle)
            {
                even = Forward.Av1ForwardTransformArithmetic<Vector256<short>>.MultiplyRound(
                    even,
                    Av1Transform1dMath.NewSqrt2,
                    Av1Transform1dMath.NewSqrt2Bits);

                odd = Forward.Av1ForwardTransformArithmetic<Vector256<short>>.MultiplyRound(
                    odd,
                    Av1Transform1dMath.NewSqrt2,
                    Av1Transform1dMath.NewSqrt2Bits);
            }

            Avx2.UnpackLow(even, odd).AsInt32().StoreUnsafe(ref scratch32, (nuint)(row * 8));
            Avx2.UnpackHigh(even, odd).AsInt32().StoreUnsafe(ref scratch32, (nuint)((row + 1) * 8));
        }

        // The Int32 and Int64 views exchange the next two index bits without allocating another temporary buffer.
        // Each group is fully consumed before its destination slots overwrite the same scratch locations.
        for (int row = 0; row < 16; row += 4)
        {
            for (int offset = 0; offset < 2; offset++)
            {
                Vector256<int> lower = Vector256.LoadUnsafe(ref scratch32, (nuint)((row + offset) * 8));
                Vector256<int> upper = Vector256.LoadUnsafe(ref scratch32, (nuint)((row + offset + 2) * 8));
                Avx2.UnpackLow(lower, upper).AsInt64().StoreUnsafe(ref scratch64, (nuint)((row + offset) * 4));
                Avx2.UnpackHigh(lower, upper).AsInt64().StoreUnsafe(ref scratch64, (nuint)((row + offset + 2) * 4));
            }
        }

        for (int row = 0; row < 16; row += 8)
        {
            for (int offset = 0; offset < 4; offset++)
            {
                Vector256<long> lower = Vector256.LoadUnsafe(ref scratch64, (nuint)((row + offset) * 4));
                Vector256<long> upper = Vector256.LoadUnsafe(ref scratch64, (nuint)((row + offset + 4) * 4));
                Avx2.UnpackLow(lower, upper).StoreUnsafe(ref scratch64, (nuint)((row + offset) * 4));
                Avx2.UnpackHigh(lower, upper).StoreUnsafe(ref scratch64, (nuint)((row + offset + 4) * 4));
            }
        }

        // Concatenating the matching 128-bit halves restores sixteen Int16 lanes per output row. The staged unpack
        // order produces a fixed row permutation, so the compile-time table maps each register to its true column.
        for (int row = 0; row < 8; row++)
        {
            Vector256<long> lower = Vector256.LoadUnsafe(ref scratch64, (nuint)(row * 4));
            Vector256<long> upper = Vector256.LoadUnsafe(ref scratch64, (nuint)((row + 8) * 4));
            Vector256<short> lowerResult = Vector256.Create(lower.GetLower(), upper.GetLower()).AsInt16();
            Vector256<short> upperResult = Vector256.Create(lower.GetUpper(), upper.GetUpper()).AsInt16();
            int lowerDestinationRow = Int16TransposeStoreOrder[row];
            int upperDestinationRow = Int16TransposeStoreOrder[row + 8];
            lowerResult.StoreUnsafe(ref destination, (nuint)(lowerDestinationRow * destinationStride));
            upperResult.StoreUnsafe(ref destination, (nuint)(upperDestinationRow * destinationStride));
        }
    }

    /// <summary>
    /// Promotes and transposes one 16-by-16 tile of signed sixteen-bit values.
    /// </summary>
    /// <param name="source">The first value of the source tile.</param>
    /// <param name="sourceStride">The number of signed sixteen-bit values between source rows.</param>
    /// <param name="destination">The first value of the destination tile.</param>
    /// <param name="destinationStride">The number of signed thirty-two-bit values between destination rows.</param>
    /// <param name="promotionBuffer">The reusable storage for the promoted source tile.</param>
    /// <param name="transposeScratch">The reusable storage for the widening transpose stages.</param>
    /// <param name="roundShift">The signed AV1 pipeline shift applied after promotion.</param>
    /// <param name="normalizeRectangle">Whether to apply the AV1 square-root-of-two rectangular normalization.</param>
    public static void Transpose16x16Int16ToInt32(
        ref short source,
        int sourceStride,
        ref int destination,
        int destinationStride,
        Span<int> promotionBuffer,
        Span<long> transposeScratch,
        int roundShift,
        bool normalizeRectangle)
    {
        ref int promotionBase = ref MemoryMarshal.GetReference(promotionBuffer);

        // The large low-bit-depth transforms widen at the axis boundary. Applying the pipeline shift after widening
        // is significant: a left shift that is valid in Int32 is not required to remain representable in Int16.
        for (int row = 0; row < 16; row++)
        {
            Vector256<short> packed = Vector256.LoadUnsafe(ref source, (nuint)(row * sourceStride));
            (Vector256<int> lower, Vector256<int> upper) = Vector256.Widen(packed);

            Vector512.Create(lower, upper).StoreUnsafe(ref promotionBase, (nuint)(row * 16));
        }

        // Once promoted, the same bounded transpose used by the high-bit-depth AVX-512 path supplies the exact
        // the reference decoder staging order and performs the axis-boundary shift in signed thirty-two-bit lanes.
        Transpose16x16Avx512(
            ref promotionBase,
            16,
            ref destination,
            destinationStride,
            transposeScratch,
            roundShift,
            normalizeRectangle);
    }

    /// <summary>
    /// Promotes and transposes one 8-by-8 tile of signed sixteen-bit values.
    /// </summary>
    /// <param name="source">The first value of the source tile.</param>
    /// <param name="sourceStride">The number of signed sixteen-bit values between source rows.</param>
    /// <param name="destination">The first value of the destination tile.</param>
    /// <param name="destinationStride">The number of signed thirty-two-bit values between destination rows.</param>
    /// <param name="promotionBuffer">The reusable storage for the promoted source tile.</param>
    /// <param name="roundShift">The signed AV1 pipeline shift applied after promotion.</param>
    /// <param name="normalizeRectangle">Whether to apply the AV1 square-root-of-two rectangular normalization.</param>
    public static void Transpose8x8Int16ToInt32(
        ref short source,
        int sourceStride,
        ref int destination,
        int destinationStride,
        Span<int> promotionBuffer,
        int roundShift,
        bool normalizeRectangle)
    {
        ref int promotionBase = ref MemoryMarshal.GetReference(promotionBuffer);

        // AVX2 processes eight Int32 transform axes at a time. Widening each packed row before the axis shift follows
        // the reference decoder's Repartition<int32_t> boundary and prevents valid Int32 intermediates from wrapping in Int16.
        for (int row = 0; row < 8; row++)
        {
            Vector128<short> packed = Vector128.LoadUnsafe(ref source, (nuint)(row * sourceStride));
            (Vector128<int> lower, Vector128<int> upper) = Vector128.Widen(packed);
            Vector256<int> promoted = Finish(Vector256.Create(lower, upper), roundShift, normalizeRectangle);

            promoted.StoreUnsafe(ref promotionBase, (nuint)(row * 8));
        }

        Transpose8x8Int32(ref promotionBase, 8, ref destination, destinationStride, 0, false);
    }

    /// <summary>
    /// Promotes and transposes one 4-by-4 tile of signed sixteen-bit values.
    /// </summary>
    /// <param name="source">The first value of the source tile.</param>
    /// <param name="sourceStride">The number of signed sixteen-bit values between source rows.</param>
    /// <param name="destination">The first value of the destination tile.</param>
    /// <param name="destinationStride">The number of signed thirty-two-bit values between destination rows.</param>
    /// <param name="promotionBuffer">The reusable storage for the promoted source tile.</param>
    /// <param name="roundShift">The signed AV1 pipeline shift applied after promotion.</param>
    /// <param name="normalizeRectangle">Whether to apply the AV1 square-root-of-two rectangular normalization.</param>
    public static void Transpose4x4Int16ToInt32(
        ref short source,
        int sourceStride,
        ref int destination,
        int destinationStride,
        Span<int> promotionBuffer,
        int roundShift,
        bool normalizeRectangle)
    {
        ref int promotionBase = ref MemoryMarshal.GetReference(promotionBuffer);

        // The portable vector path retains four independent Int32 axes. Only the lower half is populated because a
        // four-wide tile must not read the padded values belonging to its neighboring transform tile.
        for (int row = 0; row < 4; row++)
        {
            Vector128<short> packed = Load4Short(ref Unsafe.Add(ref source, row * sourceStride));
            (Vector128<int> promoted, _) = Vector128.Widen(packed);
            promoted = Finish(promoted, roundShift, normalizeRectangle);

            promoted.StoreUnsafe(ref promotionBase, (nuint)(row * 4));
        }

        Transpose4x4Int32(ref promotionBase, 4, ref destination, destinationStride, 0, false);
    }

    /// <summary>
    /// Reverses four signed thirty-two-bit lanes.
    /// </summary>
    /// <param name="value">The values to reverse.</param>
    /// <returns>The values in reverse lane order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<int> Reverse(Vector128<int> value)
        => Vector128.ShuffleNative(value, Vector128.Create(3, 2, 1, 0));

    /// <summary>
    /// Reverses eight signed sixteen-bit lanes.
    /// </summary>
    /// <param name="value">The values to reverse.</param>
    /// <returns>The values in reverse lane order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<short> Reverse(Vector128<short> value)
        => Vector128.ShuffleNative(value, Vector128.Create((short)7, 6, 5, 4, 3, 2, 1, 0));

    /// <summary>
    /// Reverses eight signed thirty-two-bit lanes.
    /// </summary>
    /// <param name="value">The values to reverse.</param>
    /// <returns>The values in reverse lane order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<int> Reverse(Vector256<int> value)
        => Vector256.ShuffleNative(value, Vector256.Create(7, 6, 5, 4, 3, 2, 1, 0));

    /// <summary>
    /// Reverses sixteen signed sixteen-bit lanes.
    /// </summary>
    /// <param name="value">The values to reverse.</param>
    /// <returns>The values in reverse lane order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<short> Reverse(Vector256<short> value)
        => Vector256.ShuffleNative(value, Vector256.Create((short)15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0));

    /// <summary>
    /// Reverses sixteen signed thirty-two-bit lanes.
    /// </summary>
    /// <param name="value">The values to reverse.</param>
    /// <returns>The values in reverse lane order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<int> Reverse(Vector512<int> value)
        => Vector512.ShuffleNative(value, Vector512.Create(15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0));

    /// <summary>
    /// Reverses thirty-two signed sixteen-bit lanes.
    /// </summary>
    /// <param name="value">The values to reverse.</param>
    /// <returns>The values in reverse lane order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<short> Reverse(Vector512<short> value)
        => Vector512.ShuffleNative(
            value,
            Vector512.Create((short)31, 30, 29, 28, 27, 26, 25, 24, 23, 22, 21, 20, 19, 18, 17, 16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0));

    /// <summary>
    /// Transposes a four-by-four matrix of signed thirty-two-bit lanes.
    /// </summary>
    /// <param name="row0">The first input row, replaced by the first output row.</param>
    /// <param name="row1">The second input row, replaced by the second output row.</param>
    /// <param name="row2">The third input row, replaced by the third output row.</param>
    /// <param name="row3">The fourth input row, replaced by the fourth output row.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Transpose(ref Vector128<int> row0, ref Vector128<int> row1, ref Vector128<int> row2, ref Vector128<int> row3)
    {
        Vector128<int> pairs01Low = Vector128_.UnpackLow(row0, row1);
        Vector128<int> pairs01High = Vector128_.UnpackHigh(row0, row1);
        Vector128<int> pairs23Low = Vector128_.UnpackLow(row2, row3);
        Vector128<int> pairs23High = Vector128_.UnpackHigh(row2, row3);

        row0 = Vector128_.UnpackLow(pairs01Low.AsInt64(), pairs23Low.AsInt64()).AsInt32();
        row1 = Vector128_.UnpackHigh(pairs01Low.AsInt64(), pairs23Low.AsInt64()).AsInt32();
        row2 = Vector128_.UnpackLow(pairs01High.AsInt64(), pairs23High.AsInt64()).AsInt32();
        row3 = Vector128_.UnpackHigh(pairs01High.AsInt64(), pairs23High.AsInt64()).AsInt32();
    }

    /// <summary>
    /// Transposes one 4-by-4 tile of signed thirty-two-bit values and applies the configured pipeline operations.
    /// </summary>
    /// <param name="source">The first value of the source tile.</param>
    /// <param name="sourceStride">The number of values between source rows.</param>
    /// <param name="destination">The first value of the destination tile.</param>
    /// <param name="destinationStride">The number of values between destination rows.</param>
    /// <param name="roundShift">The signed AV1 pipeline shift applied before transposition.</param>
    /// <param name="normalizeRectangle">Whether to apply the AV1 square-root-of-two rectangular normalization.</param>
    public static void Transpose4x4Int32(
        ref int source,
        int sourceStride,
        ref int destination,
        int destinationStride,
        int roundShift,
        bool normalizeRectangle)
    {
        Vector128<int> row0 = Finish(Vector128.LoadUnsafe(ref source), roundShift, normalizeRectangle);
        Vector128<int> row1 = Finish(Vector128.LoadUnsafe(ref source, (nuint)sourceStride), roundShift, normalizeRectangle);
        Vector128<int> row2 = Finish(Vector128.LoadUnsafe(ref source, (nuint)(2 * sourceStride)), roundShift, normalizeRectangle);
        Vector128<int> row3 = Finish(Vector128.LoadUnsafe(ref source, (nuint)(3 * sourceStride)), roundShift, normalizeRectangle);

        Transpose(ref row0, ref row1, ref row2, ref row3);
        row0.StoreUnsafe(ref destination);
        row1.StoreUnsafe(ref destination, (nuint)destinationStride);
        row2.StoreUnsafe(ref destination, (nuint)(2 * destinationStride));
        row3.StoreUnsafe(ref destination, (nuint)(3 * destinationStride));
    }

    /// <summary>
    /// Transposes an eight-by-eight matrix of signed thirty-two-bit lanes.
    /// </summary>
    /// <param name="row0">The first input row, replaced by the first output row.</param>
    /// <param name="row1">The second input row, replaced by the second output row.</param>
    /// <param name="row2">The third input row, replaced by the third output row.</param>
    /// <param name="row3">The fourth input row, replaced by the fourth output row.</param>
    /// <param name="row4">The fifth input row, replaced by the fifth output row.</param>
    /// <param name="row5">The sixth input row, replaced by the sixth output row.</param>
    /// <param name="row6">The seventh input row, replaced by the seventh output row.</param>
    /// <param name="row7">The eighth input row, replaced by the eighth output row.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Transpose(
        ref Vector256<int> row0,
        ref Vector256<int> row1,
        ref Vector256<int> row2,
        ref Vector256<int> row3,
        ref Vector256<int> row4,
        ref Vector256<int> row5,
        ref Vector256<int> row6,
        ref Vector256<int> row7)
    {
        Vector128<int> column0Lower = row0.GetLower();
        Vector128<int> column1Lower = row1.GetLower();
        Vector128<int> column2Lower = row2.GetLower();
        Vector128<int> column3Lower = row3.GetLower();
        Transpose(ref column0Lower, ref column1Lower, ref column2Lower, ref column3Lower);

        Vector128<int> column0Upper = row4.GetLower();
        Vector128<int> column1Upper = row5.GetLower();
        Vector128<int> column2Upper = row6.GetLower();
        Vector128<int> column3Upper = row7.GetLower();
        Transpose(ref column0Upper, ref column1Upper, ref column2Upper, ref column3Upper);

        Vector128<int> column4Lower = row0.GetUpper();
        Vector128<int> column5Lower = row1.GetUpper();
        Vector128<int> column6Lower = row2.GetUpper();
        Vector128<int> column7Lower = row3.GetUpper();
        Transpose(ref column4Lower, ref column5Lower, ref column6Lower, ref column7Lower);

        Vector128<int> column4Upper = row4.GetUpper();
        Vector128<int> column5Upper = row5.GetUpper();
        Vector128<int> column6Upper = row6.GetUpper();
        Vector128<int> column7Upper = row7.GetUpper();
        Transpose(ref column4Upper, ref column5Upper, ref column6Upper, ref column7Upper);

        row0 = Vector256.Create(column0Lower, column0Upper);
        row1 = Vector256.Create(column1Lower, column1Upper);
        row2 = Vector256.Create(column2Lower, column2Upper);
        row3 = Vector256.Create(column3Lower, column3Upper);
        row4 = Vector256.Create(column4Lower, column4Upper);
        row5 = Vector256.Create(column5Lower, column5Upper);
        row6 = Vector256.Create(column6Lower, column6Upper);
        row7 = Vector256.Create(column7Lower, column7Upper);
    }

    /// <summary>
    /// Transposes one 8-by-8 tile of signed thirty-two-bit values and applies the configured pipeline operations.
    /// </summary>
    /// <param name="source">The first value of the source tile.</param>
    /// <param name="sourceStride">The number of values between source rows.</param>
    /// <param name="destination">The first value of the destination tile.</param>
    /// <param name="destinationStride">The number of values between destination rows.</param>
    /// <param name="roundShift">The signed AV1 pipeline shift applied before transposition.</param>
    /// <param name="normalizeRectangle">Whether to apply the AV1 square-root-of-two rectangular normalization.</param>
    public static void Transpose8x8Int32(
        ref int source,
        int sourceStride,
        ref int destination,
        int destinationStride,
        int roundShift,
        bool normalizeRectangle)
    {
        Vector256<int> row0 = Finish(Vector256.LoadUnsafe(ref source), roundShift, normalizeRectangle);
        Vector256<int> row1 = Finish(Vector256.LoadUnsafe(ref source, (nuint)sourceStride), roundShift, normalizeRectangle);
        Vector256<int> row2 = Finish(Vector256.LoadUnsafe(ref source, (nuint)(2 * sourceStride)), roundShift, normalizeRectangle);
        Vector256<int> row3 = Finish(Vector256.LoadUnsafe(ref source, (nuint)(3 * sourceStride)), roundShift, normalizeRectangle);
        Vector256<int> row4 = Finish(Vector256.LoadUnsafe(ref source, (nuint)(4 * sourceStride)), roundShift, normalizeRectangle);
        Vector256<int> row5 = Finish(Vector256.LoadUnsafe(ref source, (nuint)(5 * sourceStride)), roundShift, normalizeRectangle);
        Vector256<int> row6 = Finish(Vector256.LoadUnsafe(ref source, (nuint)(6 * sourceStride)), roundShift, normalizeRectangle);
        Vector256<int> row7 = Finish(Vector256.LoadUnsafe(ref source, (nuint)(7 * sourceStride)), roundShift, normalizeRectangle);

        Transpose(ref row0, ref row1, ref row2, ref row3, ref row4, ref row5, ref row6, ref row7);
        row0.StoreUnsafe(ref destination);
        row1.StoreUnsafe(ref destination, (nuint)destinationStride);
        row2.StoreUnsafe(ref destination, (nuint)(2 * destinationStride));
        row3.StoreUnsafe(ref destination, (nuint)(3 * destinationStride));
        row4.StoreUnsafe(ref destination, (nuint)(4 * destinationStride));
        row5.StoreUnsafe(ref destination, (nuint)(5 * destinationStride));
        row6.StoreUnsafe(ref destination, (nuint)(6 * destinationStride));
        row7.StoreUnsafe(ref destination, (nuint)(7 * destinationStride));
    }

    /// <summary>
    /// Transposes a sixteen-by-sixteen matrix of signed thirty-two-bit values with the AVX-512 staging layout.
    /// </summary>
    /// <param name="source">The first value in the source matrix.</param>
    /// <param name="sourceStride">The number of values between source rows.</param>
    /// <param name="destination">The first value in the destination matrix.</param>
    /// <param name="destinationStride">The number of values between destination rows.</param>
    /// <param name="scratch">The caller-owned storage for sixteen vectors of signed sixty-four-bit lanes.</param>
    /// <param name="roundShift">The right shift applied with AV1 signed rounding before transposition.</param>
    /// <param name="normalizeRectangle">Whether to apply the AV1 square-root-of-two rectangular normalization.</param>
    public static void Transpose16x16Avx512(
        ref int source,
        int sourceStride,
        ref int destination,
        int destinationStride,
        Span<long> scratch,
        int roundShift,
        bool normalizeRectangle)
    {
        ref long scratchBase = ref MemoryMarshal.GetReference(scratch);

        // the reference decoder widens the lane grouping after each local interleave rather than retaining all sixteen rows in
        // registers. The bounded scratch keeps the live register set small and prevents the JIT from spilling a
        // four-stage, sixteen-register cross-vector permutation network into its own stack frame.
        for (int row = 0; row < 16; row += 2)
        {
            Vector512<int> even = Vector512.LoadUnsafe(ref source, (nuint)(row * sourceStride));
            Vector512<int> odd = Vector512.LoadUnsafe(ref source, (nuint)((row + 1) * sourceStride));
            even = RoundShift(even, roundShift);
            odd = RoundShift(odd, roundShift);

            if (normalizeRectangle)
            {
                even = Av1Transform1dMath.MultiplyRound(even, Av1Transform1dMath.NewSqrt2, Av1Transform1dMath.NewSqrt2Bits);
                odd = Av1Transform1dMath.MultiplyRound(odd, Av1Transform1dMath.NewSqrt2, Av1Transform1dMath.NewSqrt2Bits);
            }

            Avx512F.UnpackLow(even, odd).AsInt64().StoreUnsafe(ref scratchBase, (nuint)(row * 8));
            Avx512F.UnpackHigh(even, odd).AsInt64().StoreUnsafe(ref scratchBase, (nuint)((row + 1) * 8));
        }

        // The second stage exchanges the next row and column bits with 64-bit unpack operations. Each iteration
        // reads its complete four-row group before replacing that group in scratch.
        for (int row = 0; row < 16; row += 4)
        {
            for (int offset = 0; offset < 2; offset++)
            {
                Vector512<long> lower = Vector512.LoadUnsafe(ref scratchBase, (nuint)((row + offset) * 8));
                Vector512<long> upper = Vector512.LoadUnsafe(ref scratchBase, (nuint)((row + offset + 2) * 8));
                Avx512F.UnpackLow(lower, upper).StoreUnsafe(ref scratchBase, (nuint)((row + offset) * 8));
                Avx512F.UnpackHigh(lower, upper).StoreUnsafe(ref scratchBase, (nuint)((row + offset + 2) * 8));
            }
        }

        Vector512<long> evenBlockIndices = Vector512.Create(0L, 1L, 8L, 9L, 4L, 5L, 12L, 13L);
        Vector512<long> oddBlockIndices = Vector512.Create(2L, 3L, 10L, 11L, 6L, 7L, 14L, 15L);

        // Highway's LocalInterleaveEvenBlocks and LocalInterleaveOddBlocks exchange the third matrix-index bit with
        // one two-table lookup per result. The index vectors address the lower source as 0-7 and the upper as 8-15.
        for (int row = 0; row < 16; row += 8)
        {
            for (int offset = 0; offset < 4; offset++)
            {
                Vector512<long> lower = Vector512.LoadUnsafe(ref scratchBase, (nuint)((row + offset) * 8));
                Vector512<long> upper = Vector512.LoadUnsafe(ref scratchBase, (nuint)((row + offset + 4) * 8));
                Avx512F.PermuteVar8x64x2(lower, evenBlockIndices, upper).StoreUnsafe(ref scratchBase, (nuint)((row + offset) * 8));
                Avx512F.PermuteVar8x64x2(lower, oddBlockIndices, upper).StoreUnsafe(ref scratchBase, (nuint)((row + offset + 4) * 8));
            }
        }

        // The final 128-bit-block concatenations complete the transpose. The store order is the fixed permutation
        // produced by the reference decoder's three preceding local-interleave stages.
        for (int row = 0; row < 8; row++)
        {
            Vector512<long> lower = Vector512.LoadUnsafe(ref scratchBase, (nuint)(row * 8));
            Vector512<long> upper = Vector512.LoadUnsafe(ref scratchBase, (nuint)((row + 8) * 8));
            Vector512<int> lowerResult = Avx512F.Shuffle4x128(lower.AsInt32(), upper.AsInt32(), 0x44);
            Vector512<int> upperResult = Avx512F.Shuffle4x128(lower.AsInt32(), upper.AsInt32(), 0xEE);
            int lowerDestinationRow = Vector512TransposeStoreOrder[row];
            int upperDestinationRow = Vector512TransposeStoreOrder[row + 8];
            lowerResult.StoreUnsafe(ref destination, (nuint)(lowerDestinationRow * destinationStride));
            upperResult.StoreUnsafe(ref destination, (nuint)(upperDestinationRow * destinationStride));
        }
    }

    /// <summary>
    /// Transposes a sixteen-by-sixteen matrix of signed thirty-two-bit lanes.
    /// </summary>
    /// <param name="row0">The first input row, replaced by the first output row.</param>
    /// <param name="row1">The second input row, replaced by the second output row.</param>
    /// <param name="row2">The third input row, replaced by the third output row.</param>
    /// <param name="row3">The fourth input row, replaced by the fourth output row.</param>
    /// <param name="row4">The fifth input row, replaced by the fifth output row.</param>
    /// <param name="row5">The sixth input row, replaced by the sixth output row.</param>
    /// <param name="row6">The seventh input row, replaced by the seventh output row.</param>
    /// <param name="row7">The eighth input row, replaced by the eighth output row.</param>
    /// <param name="row8">The ninth input row, replaced by the ninth output row.</param>
    /// <param name="row9">The tenth input row, replaced by the tenth output row.</param>
    /// <param name="row10">The eleventh input row, replaced by the eleventh output row.</param>
    /// <param name="row11">The twelfth input row, replaced by the twelfth output row.</param>
    /// <param name="row12">The thirteenth input row, replaced by the thirteenth output row.</param>
    /// <param name="row13">The fourteenth input row, replaced by the fourteenth output row.</param>
    /// <param name="row14">The fifteenth input row, replaced by the fifteenth output row.</param>
    /// <param name="row15">The sixteenth input row, replaced by the sixteenth output row.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Transpose(
        ref Vector512<int> row0,
        ref Vector512<int> row1,
        ref Vector512<int> row2,
        ref Vector512<int> row3,
        ref Vector512<int> row4,
        ref Vector512<int> row5,
        ref Vector512<int> row6,
        ref Vector512<int> row7,
        ref Vector512<int> row8,
        ref Vector512<int> row9,
        ref Vector512<int> row10,
        ref Vector512<int> row11,
        ref Vector512<int> row12,
        ref Vector512<int> row13,
        ref Vector512<int> row14,
        ref Vector512<int> row15)
    {
        if (Avx512F.IsSupported)
        {
            // Each permutation stage exchanges one row-index bit with the matching column-index bit. After four
            // stages the vector index identifies the source column and the lane index identifies the source row.
            // This keeps the complete transpose in 512-bit registers instead of decomposing it into 128-bit tiles.
            Vector512<int> stage0Lower = Vector512.Create(0, 16, 2, 18, 4, 20, 6, 22, 8, 24, 10, 26, 12, 28, 14, 30);
            Vector512<int> stage0Upper = Vector512.Create(1, 17, 3, 19, 5, 21, 7, 23, 9, 25, 11, 27, 13, 29, 15, 31);
            Vector512<int> lowerSource = row0;
            Vector512<int> upperSource = row1;
            row0 = Avx512F.PermuteVar16x32x2(lowerSource, stage0Lower, upperSource);
            row1 = Avx512F.PermuteVar16x32x2(lowerSource, stage0Upper, upperSource);
            lowerSource = row2;
            upperSource = row3;
            row2 = Avx512F.PermuteVar16x32x2(lowerSource, stage0Lower, upperSource);
            row3 = Avx512F.PermuteVar16x32x2(lowerSource, stage0Upper, upperSource);
            lowerSource = row4;
            upperSource = row5;
            row4 = Avx512F.PermuteVar16x32x2(lowerSource, stage0Lower, upperSource);
            row5 = Avx512F.PermuteVar16x32x2(lowerSource, stage0Upper, upperSource);
            lowerSource = row6;
            upperSource = row7;
            row6 = Avx512F.PermuteVar16x32x2(lowerSource, stage0Lower, upperSource);
            row7 = Avx512F.PermuteVar16x32x2(lowerSource, stage0Upper, upperSource);
            lowerSource = row8;
            upperSource = row9;
            row8 = Avx512F.PermuteVar16x32x2(lowerSource, stage0Lower, upperSource);
            row9 = Avx512F.PermuteVar16x32x2(lowerSource, stage0Upper, upperSource);
            lowerSource = row10;
            upperSource = row11;
            row10 = Avx512F.PermuteVar16x32x2(lowerSource, stage0Lower, upperSource);
            row11 = Avx512F.PermuteVar16x32x2(lowerSource, stage0Upper, upperSource);
            lowerSource = row12;
            upperSource = row13;
            row12 = Avx512F.PermuteVar16x32x2(lowerSource, stage0Lower, upperSource);
            row13 = Avx512F.PermuteVar16x32x2(lowerSource, stage0Upper, upperSource);
            lowerSource = row14;
            upperSource = row15;
            row14 = Avx512F.PermuteVar16x32x2(lowerSource, stage0Lower, upperSource);
            row15 = Avx512F.PermuteVar16x32x2(lowerSource, stage0Upper, upperSource);

            Vector512<int> stage1Lower = Vector512.Create(0, 1, 16, 17, 4, 5, 20, 21, 8, 9, 24, 25, 12, 13, 28, 29);
            Vector512<int> stage1Upper = Vector512.Create(2, 3, 18, 19, 6, 7, 22, 23, 10, 11, 26, 27, 14, 15, 30, 31);
            lowerSource = row0;
            upperSource = row2;
            row0 = Avx512F.PermuteVar16x32x2(lowerSource, stage1Lower, upperSource);
            row2 = Avx512F.PermuteVar16x32x2(lowerSource, stage1Upper, upperSource);
            lowerSource = row1;
            upperSource = row3;
            row1 = Avx512F.PermuteVar16x32x2(lowerSource, stage1Lower, upperSource);
            row3 = Avx512F.PermuteVar16x32x2(lowerSource, stage1Upper, upperSource);
            lowerSource = row4;
            upperSource = row6;
            row4 = Avx512F.PermuteVar16x32x2(lowerSource, stage1Lower, upperSource);
            row6 = Avx512F.PermuteVar16x32x2(lowerSource, stage1Upper, upperSource);
            lowerSource = row5;
            upperSource = row7;
            row5 = Avx512F.PermuteVar16x32x2(lowerSource, stage1Lower, upperSource);
            row7 = Avx512F.PermuteVar16x32x2(lowerSource, stage1Upper, upperSource);
            lowerSource = row8;
            upperSource = row10;
            row8 = Avx512F.PermuteVar16x32x2(lowerSource, stage1Lower, upperSource);
            row10 = Avx512F.PermuteVar16x32x2(lowerSource, stage1Upper, upperSource);
            lowerSource = row9;
            upperSource = row11;
            row9 = Avx512F.PermuteVar16x32x2(lowerSource, stage1Lower, upperSource);
            row11 = Avx512F.PermuteVar16x32x2(lowerSource, stage1Upper, upperSource);
            lowerSource = row12;
            upperSource = row14;
            row12 = Avx512F.PermuteVar16x32x2(lowerSource, stage1Lower, upperSource);
            row14 = Avx512F.PermuteVar16x32x2(lowerSource, stage1Upper, upperSource);
            lowerSource = row13;
            upperSource = row15;
            row13 = Avx512F.PermuteVar16x32x2(lowerSource, stage1Lower, upperSource);
            row15 = Avx512F.PermuteVar16x32x2(lowerSource, stage1Upper, upperSource);

            Vector512<int> stage2Lower = Vector512.Create(0, 1, 2, 3, 16, 17, 18, 19, 8, 9, 10, 11, 24, 25, 26, 27);
            Vector512<int> stage2Upper = Vector512.Create(4, 5, 6, 7, 20, 21, 22, 23, 12, 13, 14, 15, 28, 29, 30, 31);
            lowerSource = row0;
            upperSource = row4;
            row0 = Avx512F.PermuteVar16x32x2(lowerSource, stage2Lower, upperSource);
            row4 = Avx512F.PermuteVar16x32x2(lowerSource, stage2Upper, upperSource);
            lowerSource = row1;
            upperSource = row5;
            row1 = Avx512F.PermuteVar16x32x2(lowerSource, stage2Lower, upperSource);
            row5 = Avx512F.PermuteVar16x32x2(lowerSource, stage2Upper, upperSource);
            lowerSource = row2;
            upperSource = row6;
            row2 = Avx512F.PermuteVar16x32x2(lowerSource, stage2Lower, upperSource);
            row6 = Avx512F.PermuteVar16x32x2(lowerSource, stage2Upper, upperSource);
            lowerSource = row3;
            upperSource = row7;
            row3 = Avx512F.PermuteVar16x32x2(lowerSource, stage2Lower, upperSource);
            row7 = Avx512F.PermuteVar16x32x2(lowerSource, stage2Upper, upperSource);
            lowerSource = row8;
            upperSource = row12;
            row8 = Avx512F.PermuteVar16x32x2(lowerSource, stage2Lower, upperSource);
            row12 = Avx512F.PermuteVar16x32x2(lowerSource, stage2Upper, upperSource);
            lowerSource = row9;
            upperSource = row13;
            row9 = Avx512F.PermuteVar16x32x2(lowerSource, stage2Lower, upperSource);
            row13 = Avx512F.PermuteVar16x32x2(lowerSource, stage2Upper, upperSource);
            lowerSource = row10;
            upperSource = row14;
            row10 = Avx512F.PermuteVar16x32x2(lowerSource, stage2Lower, upperSource);
            row14 = Avx512F.PermuteVar16x32x2(lowerSource, stage2Upper, upperSource);
            lowerSource = row11;
            upperSource = row15;
            row11 = Avx512F.PermuteVar16x32x2(lowerSource, stage2Lower, upperSource);
            row15 = Avx512F.PermuteVar16x32x2(lowerSource, stage2Upper, upperSource);

            Vector512<int> stage3Lower = Vector512.Create(0, 1, 2, 3, 4, 5, 6, 7, 16, 17, 18, 19, 20, 21, 22, 23);
            Vector512<int> stage3Upper = Vector512.Create(8, 9, 10, 11, 12, 13, 14, 15, 24, 25, 26, 27, 28, 29, 30, 31);
            lowerSource = row0;
            upperSource = row8;
            row0 = Avx512F.PermuteVar16x32x2(lowerSource, stage3Lower, upperSource);
            row8 = Avx512F.PermuteVar16x32x2(lowerSource, stage3Upper, upperSource);
            lowerSource = row1;
            upperSource = row9;
            row1 = Avx512F.PermuteVar16x32x2(lowerSource, stage3Lower, upperSource);
            row9 = Avx512F.PermuteVar16x32x2(lowerSource, stage3Upper, upperSource);
            lowerSource = row2;
            upperSource = row10;
            row2 = Avx512F.PermuteVar16x32x2(lowerSource, stage3Lower, upperSource);
            row10 = Avx512F.PermuteVar16x32x2(lowerSource, stage3Upper, upperSource);
            lowerSource = row3;
            upperSource = row11;
            row3 = Avx512F.PermuteVar16x32x2(lowerSource, stage3Lower, upperSource);
            row11 = Avx512F.PermuteVar16x32x2(lowerSource, stage3Upper, upperSource);
            lowerSource = row4;
            upperSource = row12;
            row4 = Avx512F.PermuteVar16x32x2(lowerSource, stage3Lower, upperSource);
            row12 = Avx512F.PermuteVar16x32x2(lowerSource, stage3Upper, upperSource);
            lowerSource = row5;
            upperSource = row13;
            row5 = Avx512F.PermuteVar16x32x2(lowerSource, stage3Lower, upperSource);
            row13 = Avx512F.PermuteVar16x32x2(lowerSource, stage3Upper, upperSource);
            lowerSource = row6;
            upperSource = row14;
            row6 = Avx512F.PermuteVar16x32x2(lowerSource, stage3Lower, upperSource);
            row14 = Avx512F.PermuteVar16x32x2(lowerSource, stage3Upper, upperSource);
            lowerSource = row7;
            upperSource = row15;
            row7 = Avx512F.PermuteVar16x32x2(lowerSource, stage3Lower, upperSource);
            row15 = Avx512F.PermuteVar16x32x2(lowerSource, stage3Upper, upperSource);
            return;
        }

        // A 16x16 transpose consists of four independent 8x8 quadrants. Reusing the established 256-bit transpose
        // keeps the portable layout path branch-free while the transform arithmetic itself remains in 512-bit lanes.
        // Preserve the bottom-left quadrant before row8-row15 become upper-column output storage. Emitting those upper
        // columns first avoids keeping all four quadrants live across the complete operation.
        Vector256<int> lowerBottom0 = row8.GetLower();
        Vector256<int> lowerBottom1 = row9.GetLower();
        Vector256<int> lowerBottom2 = row10.GetLower();
        Vector256<int> lowerBottom3 = row11.GetLower();
        Vector256<int> lowerBottom4 = row12.GetLower();
        Vector256<int> lowerBottom5 = row13.GetLower();
        Vector256<int> lowerBottom6 = row14.GetLower();
        Vector256<int> lowerBottom7 = row15.GetLower();
        Vector256<int> upperTop0 = row0.GetUpper();
        Vector256<int> upperTop1 = row1.GetUpper();
        Vector256<int> upperTop2 = row2.GetUpper();
        Vector256<int> upperTop3 = row3.GetUpper();
        Vector256<int> upperTop4 = row4.GetUpper();
        Vector256<int> upperTop5 = row5.GetUpper();
        Vector256<int> upperTop6 = row6.GetUpper();
        Vector256<int> upperTop7 = row7.GetUpper();
        Vector256<int> upperBottom0 = row8.GetUpper();
        Vector256<int> upperBottom1 = row9.GetUpper();
        Vector256<int> upperBottom2 = row10.GetUpper();
        Vector256<int> upperBottom3 = row11.GetUpper();
        Vector256<int> upperBottom4 = row12.GetUpper();
        Vector256<int> upperBottom5 = row13.GetUpper();
        Vector256<int> upperBottom6 = row14.GetUpper();
        Vector256<int> upperBottom7 = row15.GetUpper();

        Transpose(ref upperTop0, ref upperTop1, ref upperTop2, ref upperTop3, ref upperTop4, ref upperTop5, ref upperTop6, ref upperTop7);
        Transpose(ref upperBottom0, ref upperBottom1, ref upperBottom2, ref upperBottom3, ref upperBottom4, ref upperBottom5, ref upperBottom6, ref upperBottom7);

        row8 = Vector512.Create(upperTop0, upperBottom0);
        row9 = Vector512.Create(upperTop1, upperBottom1);
        row10 = Vector512.Create(upperTop2, upperBottom2);
        row11 = Vector512.Create(upperTop3, upperBottom3);
        row12 = Vector512.Create(upperTop4, upperBottom4);
        row13 = Vector512.Create(upperTop5, upperBottom5);
        row14 = Vector512.Create(upperTop6, upperBottom6);
        row15 = Vector512.Create(upperTop7, upperBottom7);

        Vector256<int> lowerTop0 = row0.GetLower();
        Vector256<int> lowerTop1 = row1.GetLower();
        Vector256<int> lowerTop2 = row2.GetLower();
        Vector256<int> lowerTop3 = row3.GetLower();
        Vector256<int> lowerTop4 = row4.GetLower();
        Vector256<int> lowerTop5 = row5.GetLower();
        Vector256<int> lowerTop6 = row6.GetLower();
        Vector256<int> lowerTop7 = row7.GetLower();

        Transpose(ref lowerTop0, ref lowerTop1, ref lowerTop2, ref lowerTop3, ref lowerTop4, ref lowerTop5, ref lowerTop6, ref lowerTop7);
        Transpose(ref lowerBottom0, ref lowerBottom1, ref lowerBottom2, ref lowerBottom3, ref lowerBottom4, ref lowerBottom5, ref lowerBottom6, ref lowerBottom7);

        row0 = Vector512.Create(lowerTop0, lowerBottom0);
        row1 = Vector512.Create(lowerTop1, lowerBottom1);
        row2 = Vector512.Create(lowerTop2, lowerBottom2);
        row3 = Vector512.Create(lowerTop3, lowerBottom3);
        row4 = Vector512.Create(lowerTop4, lowerBottom4);
        row5 = Vector512.Create(lowerTop5, lowerBottom5);
        row6 = Vector512.Create(lowerTop6, lowerBottom6);
        row7 = Vector512.Create(lowerTop7, lowerBottom7);
    }

    /// <summary>
    /// Applies the terminal operations which the reference decoder performs before transposing a signed sixteen-bit tile.
    /// </summary>
    /// <param name="value">The packed transform values.</param>
    /// <param name="roundShift">The signed AV1 pipeline shift.</param>
    /// <param name="normalizeRectangle">Whether to apply square-root-of-two rectangular normalization.</param>
    /// <returns>The shifted and normalized values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<short> Finish(Vector128<short> value, int roundShift, bool normalizeRectangle)
    {
        value = RoundShift(value, roundShift);
        return normalizeRectangle
            ? Forward.Av1ForwardTransformArithmetic<Vector128<short>>.MultiplyRound(
                value,
                Av1Transform1dMath.NewSqrt2,
                Av1Transform1dMath.NewSqrt2Bits)
            : value;
    }

    /// <summary>
    /// Applies the terminal operations which the reference decoder performs before transposing four signed thirty-two-bit lanes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> Finish(Vector128<int> value, int roundShift, bool normalizeRectangle)
    {
        value = RoundShift(value, roundShift);
        return normalizeRectangle
            ? Av1Transform1dMath.MultiplyRound(value, Av1Transform1dMath.NewSqrt2, Av1Transform1dMath.NewSqrt2Bits)
            : value;
    }

    /// <summary>
    /// Applies the terminal operations which the reference decoder performs before transposing eight signed thirty-two-bit lanes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> Finish(Vector256<int> value, int roundShift, bool normalizeRectangle)
    {
        value = RoundShift(value, roundShift);
        return normalizeRectangle
            ? Av1Transform1dMath.MultiplyRound(value, Av1Transform1dMath.NewSqrt2, Av1Transform1dMath.NewSqrt2Bits)
            : value;
    }

    /// <summary>
    /// Loads four signed sixteen-bit values without reading outside the source tile.
    /// </summary>
    /// <param name="source">The first source value.</param>
    /// <returns>The four source values in the lower vector lanes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<short> Load4Short(ref short source)
        => Vector128.Create(Unsafe.As<short, ulong>(ref source), 0UL).AsInt16();

    /// <summary>
    /// Stores the lower four signed sixteen-bit lanes without writing outside the destination tile.
    /// </summary>
    /// <param name="value">The packed lower lanes.</param>
    /// <param name="destination">The first destination value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Store4Int16(ulong value, ref short destination)
        => Unsafe.As<short, ulong>(ref destination) = value;
}
