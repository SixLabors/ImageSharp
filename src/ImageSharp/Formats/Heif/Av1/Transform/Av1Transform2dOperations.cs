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
    /// Reverses four signed thirty-two-bit lanes.
    /// </summary>
    /// <param name="value">The values to reverse.</param>
    /// <returns>The values in reverse lane order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<int> Reverse(Vector128<int> value)
        => Vector128.ShuffleNative(value, Vector128.Create(3, 2, 1, 0));

    /// <summary>
    /// Reverses eight signed thirty-two-bit lanes.
    /// </summary>
    /// <param name="value">The values to reverse.</param>
    /// <returns>The values in reverse lane order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<int> Reverse(Vector256<int> value)
        => Vector256.ShuffleNative(value, Vector256.Create(7, 6, 5, 4, 3, 2, 1, 0));

    /// <summary>
    /// Reverses sixteen signed thirty-two-bit lanes.
    /// </summary>
    /// <param name="value">The values to reverse.</param>
    /// <returns>The values in reverse lane order.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<int> Reverse(Vector512<int> value)
        => Vector512.ShuffleNative(value, Vector512.Create(15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0));

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
}
