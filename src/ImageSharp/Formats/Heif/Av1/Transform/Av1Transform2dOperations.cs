// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <summary>
/// Provides the SIMD data-layout operations shared by AV1 two-dimensional transforms.
/// </summary>
internal static class Av1Transform2dOperations
{
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
    /// Reverses four signed thirty-two-bit lanes.
    /// </summary>
    /// <param name="value">The values to reverse.</param>
    /// <returns>The values in reverse lane order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<int> Reverse(Vector128<int> value)
        => Vector128.Shuffle(value, Vector128.Create(3, 2, 1, 0));

    /// <summary>
    /// Reverses eight signed thirty-two-bit lanes.
    /// </summary>
    /// <param name="value">The values to reverse.</param>
    /// <returns>The values in reverse lane order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<int> Reverse(Vector256<int> value)
        => Vector256.Shuffle(value, Vector256.Create(7, 6, 5, 4, 3, 2, 1, 0));

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
}
