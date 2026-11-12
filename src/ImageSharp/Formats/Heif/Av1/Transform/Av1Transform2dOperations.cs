// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Provides the SIMD data-layout operations shared by AV1 two-dimensional transforms.
/// </summary>
/// <remarks>
/// The one-dimensional operators expect one transform position per vector and one independent axis per lane. These routines transpose rectangular sample tiles
/// into that structure, then transpose the completed axes back to raster order. Thus every shuffle is an index-bit exchange between row and column coordinates.
/// No shuffle changes the signed fixed-point sample representation.
/// </remarks>
internal static class Av1Transform2dOperations
{
    /// <summary>
    /// The destination row of each result of the final AVX-512 16-by-16 transpose concatenation.
    /// </summary>
    private static readonly byte[] Vector512TransposeStoreOrder = [0, 2, 1, 3, 4, 6, 5, 7, 8, 10, 9, 11, 12, 14, 13, 15];

    /// <summary>
    /// The destination row of each result of the final sixteen-bit 16-by-16 transpose concatenation.
    /// </summary>
    private static readonly byte[] Int16TransposeStoreOrder = [0, 4, 2, 6, 1, 5, 3, 7, 8, 12, 10, 14, 9, 13, 11, 15];

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
            // Conformant low-bit-depth stage ranges leave room for the rounding bias, so the code uses a wrapping add. A saturating add can change the
            // normative result.
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

        // Only the lower four lanes belong to the tile. Interleaving at Int16 and Int32 granularity exchanges the two row-index bits with the corresponding
        // column-index bits without touching adjacent padded storage.
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

        // Three unpack rounds at Int16, Int32 and Int64 granularity each exchange one row-index bit with one column-index bit.
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
    /// Transposes one 16-by-16 tile of signed sixteen-bit values and applies the configured pipeline operations.
    /// </summary>
    /// <param name="source">The first value of the source tile.</param>
    /// <param name="sourceStride">The number of signed sixteen-bit values between source rows.</param>
    /// <param name="destination">The first value of the destination tile.</param>
    /// <param name="destinationStride">The number of signed sixteen-bit values between destination rows.</param>
    /// <param name="transposeStorage">The reusable storage for the transpose stages at Int32 and Int64 granularity.</param>
    /// <param name="roundShift">The signed AV1 pipeline shift applied before transposition.</param>
    /// <param name="normalizeRectangle">Whether to apply the AV1 square-root-of-two rectangular normalization.</param>
    public static void Transpose16x16Int16(
        ref short source,
        int sourceStride,
        ref short destination,
        int destinationStride,
        Span<long> transposeStorage,
        int roundShift,
        bool normalizeRectangle)
    {
        ref long transposeBase64 = ref MemoryMarshal.GetReference(transposeStorage);
        ref int transposeBase32 = ref Unsafe.As<long, int>(ref transposeBase64);

        // The unpack of each pair of adjacent rows stores two Int16 values in each Int32 element. This step is a bitwise reinterpretation, not a sign
        // extension. It keeps all sixteen source bits and exchanges the lowest row-index bit with a column-index bit.
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

            Vector256_.UnpackLow(even, odd).AsInt32().StoreUnsafe(ref transposeBase32, (nuint)(row * 8));
            Vector256_.UnpackHigh(even, odd).AsInt32().StoreUnsafe(ref transposeBase32, (nuint)((row + 1) * 8));
        }

        // The next two loops exchange the next two index bits with Int32 and Int64 unpacks in the same buffer, so no other temporary buffer is necessary. Each
        // pass reads both rows of a pair before it writes them back to the same locations.
        for (int row = 0; row < 16; row += 4)
        {
            for (int offset = 0; offset < 2; offset++)
            {
                Vector256<int> lower = Vector256.LoadUnsafe(ref transposeBase32, (nuint)((row + offset) * 8));
                Vector256<int> upper = Vector256.LoadUnsafe(ref transposeBase32, (nuint)((row + offset + 2) * 8));
                Vector256_.UnpackLow(lower, upper).AsInt64().StoreUnsafe(ref transposeBase64, (nuint)((row + offset) * 4));
                Vector256_.UnpackHigh(lower, upper).AsInt64().StoreUnsafe(ref transposeBase64, (nuint)((row + offset + 2) * 4));
            }
        }

        for (int row = 0; row < 16; row += 8)
        {
            for (int offset = 0; offset < 4; offset++)
            {
                Vector256<long> lower = Vector256.LoadUnsafe(ref transposeBase64, (nuint)((row + offset) * 4));
                Vector256<long> upper = Vector256.LoadUnsafe(ref transposeBase64, (nuint)((row + offset + 4) * 4));
                Vector256_.UnpackLow(lower, upper).StoreUnsafe(ref transposeBase64, (nuint)((row + offset) * 4));
                Vector256_.UnpackHigh(lower, upper).StoreUnsafe(ref transposeBase64, (nuint)((row + offset + 4) * 4));
            }
        }

        // Concatenating the matching 128-bit halves restores sixteen Int16 lanes per output row. The staged unpack order produces a fixed row permutation, so a
        // static table maps each result to its destination row.
        for (int row = 0; row < 8; row++)
        {
            Vector256<long> lower = Vector256.LoadUnsafe(ref transposeBase64, (nuint)(row * 4));
            Vector256<long> upper = Vector256.LoadUnsafe(ref transposeBase64, (nuint)((row + 8) * 4));
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
    /// <param name="transposeStorage">The reusable storage for the transpose stages at Int64 granularity.</param>
    /// <param name="roundShift">The signed AV1 pipeline shift applied after promotion.</param>
    /// <param name="normalizeRectangle">Whether to apply the AV1 square-root-of-two rectangular normalization.</param>
    public static void Transpose16x16Int16ToInt32(
        ref short source,
        int sourceStride,
        ref int destination,
        int destinationStride,
        Span<int> promotionBuffer,
        Span<long> transposeStorage,
        int roundShift,
        bool normalizeRectangle)
    {
        ref int promotionBase = ref MemoryMarshal.GetReference(promotionBuffer);

        // The large low-bit-depth transforms widen at the axis boundary. The pipeline shift must come after the widening, because a left shift that is valid in
        // Int32 can overflow Int16.
        for (int row = 0; row < 16; row++)
        {
            Vector256<short> packed = Vector256.LoadUnsafe(ref source, (nuint)(row * sourceStride));
            (Vector256<int> lower, Vector256<int> upper) = Vector256.Widen(packed);

            Vector512.Create(lower, upper).StoreUnsafe(ref promotionBase, (nuint)(row * 16));
        }

        // After the promotion, the bounded transpose of the high-bit-depth AVX-512 path gives the exact staging order. It also applies the axis-boundary shift
        // in signed thirty-two-bit lanes.
        Transpose16x16Avx512(
            ref promotionBase,
            16,
            ref destination,
            destinationStride,
            transposeStorage,
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

        // AVX2 processes eight Int32 transform axes at a time. The code widens each packed row before the axis shift. This puts the change to Int32 at the axis
        // boundary, and valid Int32 intermediates do not wrap in Int16.
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

        // The portable vector path keeps four independent Int32 axes. The code loads only four values per row, because a four-wide tile must not read the
        // padded values of the neighboring transform tile.
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

        SimdUtils.Transpose(ref row0, ref row1, ref row2, ref row3);
        row0.StoreUnsafe(ref destination);
        row1.StoreUnsafe(ref destination, (nuint)destinationStride);
        row2.StoreUnsafe(ref destination, (nuint)(2 * destinationStride));
        row3.StoreUnsafe(ref destination, (nuint)(3 * destinationStride));
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

        SimdUtils.Transpose(ref row0, ref row1, ref row2, ref row3, ref row4, ref row5, ref row6, ref row7);
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
    /// <param name="transposeStorage">The caller-owned storage for sixteen vectors of signed sixty-four-bit lanes.</param>
    /// <param name="roundShift">The signed AV1 pipeline shift applied before transposition.</param>
    /// <param name="normalizeRectangle">Whether to apply the AV1 square-root-of-two rectangular normalization.</param>
    public static void Transpose16x16Avx512(
        ref int source,
        int sourceStride,
        ref int destination,
        int destinationStride,
        Span<long> transposeStorage,
        int roundShift,
        bool normalizeRectangle)
    {
        ref long transposeBase = ref MemoryMarshal.GetReference(transposeStorage);

        // The lane grouping widens after each local interleave, and the sixteen rows do not stay in registers. The bounded buffer keeps the live register set
        // small. Thus the JIT does not spill a four-stage, sixteen-register cross-vector permutation network into its own stack frame.
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

            Vector512_.UnpackLow(even, odd).AsInt64().StoreUnsafe(ref transposeBase, (nuint)(row * 8));
            Vector512_.UnpackHigh(even, odd).AsInt64().StoreUnsafe(ref transposeBase, (nuint)((row + 1) * 8));
        }

        // The second stage exchanges the next row and column bits with 64-bit unpack operations. Each inner pass reads two rows before it writes them back to
        // the same locations.
        for (int row = 0; row < 16; row += 4)
        {
            for (int offset = 0; offset < 2; offset++)
            {
                Vector512<long> lower = Vector512.LoadUnsafe(ref transposeBase, (nuint)((row + offset) * 8));
                Vector512<long> upper = Vector512.LoadUnsafe(ref transposeBase, (nuint)((row + offset + 2) * 8));
                Vector512_.UnpackLow(lower, upper).StoreUnsafe(ref transposeBase, (nuint)((row + offset) * 8));
                Vector512_.UnpackHigh(lower, upper).StoreUnsafe(ref transposeBase, (nuint)((row + offset + 2) * 8));
            }
        }

        Vector512<long> evenBlockIndices = Vector512.Create(0L, 1L, 8L, 9L, 4L, 5L, 12L, 13L);
        Vector512<long> oddBlockIndices = Vector512.Create(2L, 3L, 10L, 11L, 6L, 7L, 14L, 15L);

        // The even-block and odd-block interleaves exchange the third matrix-index bit with one two-table lookup per result. The index vectors address the
        // lower source as 0-7 and the upper source as 8-15.
        for (int row = 0; row < 16; row += 8)
        {
            for (int offset = 0; offset < 4; offset++)
            {
                Vector512<long> lower = Vector512.LoadUnsafe(ref transposeBase, (nuint)((row + offset) * 8));
                Vector512<long> upper = Vector512.LoadUnsafe(ref transposeBase, (nuint)((row + offset + 4) * 8));
                Vector512_.PermuteVar8x64x2(lower, evenBlockIndices, upper).StoreUnsafe(ref transposeBase, (nuint)((row + offset) * 8));
                Vector512_.PermuteVar8x64x2(lower, oddBlockIndices, upper).StoreUnsafe(ref transposeBase, (nuint)((row + offset + 4) * 8));
            }
        }

        // The final 128-bit-block concatenations complete the transpose. The store order is the fixed permutation that the three preceding local-interleave
        // stages produce.
        for (int row = 0; row < 8; row++)
        {
            Vector512<long> lower = Vector512.LoadUnsafe(ref transposeBase, (nuint)(row * 8));
            Vector512<long> upper = Vector512.LoadUnsafe(ref transposeBase, (nuint)((row + 8) * 8));
            Vector512<int> lowerResult = Vector512_.Shuffle4x128(lower.AsInt32(), upper.AsInt32(), 0x44);
            Vector512<int> upperResult = Vector512_.Shuffle4x128(lower.AsInt32(), upper.AsInt32(), 0xEE);
            int lowerDestinationRow = Vector512TransposeStoreOrder[row];
            int upperDestinationRow = Vector512TransposeStoreOrder[row + 8];
            lowerResult.StoreUnsafe(ref destination, (nuint)(lowerDestinationRow * destinationStride));
            upperResult.StoreUnsafe(ref destination, (nuint)(upperDestinationRow * destinationStride));
        }
    }

    /// <summary>
    /// Applies the final stage shift and the rectangular normalization before the transpose of a signed sixteen-bit tile.
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
    /// Applies the final stage shift and the rectangular normalization before the transpose of four signed thirty-two-bit lanes.
    /// </summary>
    /// <param name="value">The transform values.</param>
    /// <param name="roundShift">The signed AV1 pipeline shift.</param>
    /// <param name="normalizeRectangle">Whether to apply square-root-of-two rectangular normalization.</param>
    /// <returns>The shifted and normalized values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> Finish(Vector128<int> value, int roundShift, bool normalizeRectangle)
    {
        value = RoundShift(value, roundShift);
        return normalizeRectangle
            ? Av1Transform1dMath.MultiplyRound(value, Av1Transform1dMath.NewSqrt2, Av1Transform1dMath.NewSqrt2Bits)
            : value;
    }

    /// <summary>
    /// Applies the final stage shift and the rectangular normalization before the transpose of eight signed thirty-two-bit lanes.
    /// </summary>
    /// <param name="value">The transform values.</param>
    /// <param name="roundShift">The signed AV1 pipeline shift.</param>
    /// <param name="normalizeRectangle">Whether to apply square-root-of-two rectangular normalization.</param>
    /// <returns>The shifted and normalized values.</returns>
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
