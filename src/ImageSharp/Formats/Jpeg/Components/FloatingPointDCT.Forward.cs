// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

/// <content>
/// Contains the encoder stage: level shift, forward transform, quantization and zig-zag reorder.
/// </content>
internal static partial class FloatingPointDCT
{
    // The encoder samples are 8-bit values in [0, 255]. The forward transform needs values centered on zero.
    private const float EncoderLevelShift = 128F;

    // Each width does the complete stage in registers:
    // 1. The code loads eight sample rows and subtracts the level shift.
    // 2. The code applies the two-dimensional forward transform. The output is in transposed order.
    // 3. The code multiplies each row by the quantization table row. It rounds to the nearest integer,
    //    with ties to the even integer. Then it narrows to 16 bits with saturation.
    // 4. The transposing zig-zag operator reorders the block. The result is in the standard coefficient order.
    // 5. The code stores the block once.

    /// <summary>
    /// Subtracts the level shift from one block of samples, applies the forward transform and quantizes the coefficients.
    /// </summary>
    /// <param name="source">The first sample of the block. The method reads 8 rows of 8 samples.</param>
    /// <param name="sourceStride">The distance between two source rows, in samples.</param>
    /// <param name="quantizationTable">The quantization table, adjusted by <see cref="AdjustToFDCT"/>.</param>
    /// <param name="destination">The destination for the quantized coefficients in zig-zag order.</param>
    public static void TransformFDCT(ref float source, int sourceStride, ref Block8x8F quantizationTable, ref Block8x8 destination)
    {
        nuint stride = (uint)sourceStride;
        if (Vector256.IsHardwareAccelerated)
        {
            ForwardVector256(ref source, stride, ref quantizationTable, ref destination);
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            ForwardVector128(ref source, stride, ref quantizationTable, ref destination);
        }
        else
        {
            ForwardScalar(ref source, stride, ref quantizationTable, ref destination);
        }
    }

    /// <summary>
    /// Subtracts the level shift from a row of sample blocks, applies the forward transform and quantizes the coefficients.
    /// </summary>
    /// <remarks>
    /// Block <c>i</c> reads columns <c>8 * i</c> to <c>8 * i + 7</c> of 8 source rows.
    /// The result is the same as one <see cref="TransformFDCT(ref float, int, ref Block8x8F, ref Block8x8)"/> call for each block.
    /// </remarks>
    /// <param name="source">The first sample of the source.</param>
    /// <param name="sourceStride">The distance between two source rows, in samples.</param>
    /// <param name="quantizationTable">The quantization table, adjusted by <see cref="AdjustToFDCT"/>.</param>
    /// <param name="destination">The destination for the quantized blocks in zig-zag order.</param>
    public static void TransformFDCT(
        ref float source,
        int sourceStride,
        ref Block8x8F quantizationTable,
        Span<Block8x8> destination)
    {
        int i = 0;
        if (Vector512.IsHardwareAccelerated)
        {
            // Two adjacent blocks fill one Vector512 row.
            nuint stride = (uint)sourceStride;
            for (; i <= destination.Length - 2; i += 2)
            {
                ForwardVector512(ref Unsafe.Add(ref source, (uint)i * 8), stride, ref quantizationTable, ref destination[i], ref destination[i + 1]);
            }
        }

        for (; i < destination.Length; i++)
        {
            TransformFDCT(ref Unsafe.Add(ref source, (uint)i * 8), sourceStride, ref quantizationTable, ref destination[i]);
        }
    }

    /// <summary>
    /// The <see cref="Vector512{Single}"/> version of the encoder stage for two blocks.
    /// </summary>
    /// <remarks>
    /// The two blocks are adjacent in the source. Thus, one 16-sample load gives the same row of both blocks.
    /// The low 256 bits of each register hold the row of <paramref name="first"/>.
    /// The high 256 bits hold the row of <paramref name="second"/>.
    /// </remarks>
    /// <param name="source">The first sample of the first block.</param>
    /// <param name="stride">The distance between two source rows, in samples.</param>
    /// <param name="quantizationTable">The quantization table.</param>
    /// <param name="first">The destination for the quantized coefficients of the first block.</param>
    /// <param name="second">The destination for the quantized coefficients of the second block.</param>
    private static void ForwardVector512(
        ref float source,
        nuint stride,
        ref Block8x8F quantizationTable,
        ref Block8x8 first,
        ref Block8x8 second)
    {
        Vector512<float> shift = Vector512.Create(EncoderLevelShift);
        Vector512<float> v0 = Vector512.LoadUnsafe(ref source, 0) - shift;
        Vector512<float> v1 = Vector512.LoadUnsafe(ref source, stride) - shift;
        Vector512<float> v2 = Vector512.LoadUnsafe(ref source, 2 * stride) - shift;
        Vector512<float> v3 = Vector512.LoadUnsafe(ref source, 3 * stride) - shift;
        Vector512<float> v4 = Vector512.LoadUnsafe(ref source, 4 * stride) - shift;
        Vector512<float> v5 = Vector512.LoadUnsafe(ref source, 5 * stride) - shift;
        Vector512<float> v6 = Vector512.LoadUnsafe(ref source, 6 * stride) - shift;
        Vector512<float> v7 = Vector512.LoadUnsafe(ref source, 7 * stride) - shift;

        Transform<ForwardTransformOperator>(ref v0, ref v1, ref v2, ref v3, ref v4, ref v5, ref v6, ref v7);

        // Each quantized row pair holds row r of the first block in its low 8 values. It holds row r of the second block in its high 8 values.
        Vector256<short> q0 = Quantize(v0, quantizationTable.V256_0);
        Vector256<short> q1 = Quantize(v1, quantizationTable.V256_1);
        Vector256<short> q2 = Quantize(v2, quantizationTable.V256_2);
        Vector256<short> q3 = Quantize(v3, quantizationTable.V256_3);
        Vector256<short> q4 = Quantize(v4, quantizationTable.V256_4);
        Vector256<short> q5 = Quantize(v5, quantizationTable.V256_5);
        Vector256<short> q6 = Quantize(v6, quantizationTable.V256_6);
        Vector256<short> q7 = Quantize(v7, quantizationTable.V256_7);

        // The zig-zag operator takes one block as two registers of four rows each.
        // The low halves of the row pairs make the first block. The high halves make the second block.
        Vector512<short> first0123 = Vector512.Create(Vector256.Create(q0.GetLower(), q1.GetLower()), Vector256.Create(q2.GetLower(), q3.GetLower()));
        Vector512<short> first4567 = Vector512.Create(Vector256.Create(q4.GetLower(), q5.GetLower()), Vector256.Create(q6.GetLower(), q7.GetLower()));
        Vector512<short> second0123 = Vector512.Create(Vector256.Create(q0.GetUpper(), q1.GetUpper()), Vector256.Create(q2.GetUpper(), q3.GetUpper()));
        Vector512<short> second4567 = Vector512.Create(Vector256.Create(q4.GetUpper(), q5.GetUpper()), Vector256.Create(q6.GetUpper(), q7.GetUpper()));
        ZigZag.TransposingZigZagOperator.Invoke(ref first0123, ref first4567);
        ZigZag.TransposingZigZagOperator.Invoke(ref second0123, ref second4567);

        ref short firstBase = ref Unsafe.As<Block8x8, short>(ref first);
        ref short secondBase = ref Unsafe.As<Block8x8, short>(ref second);
        first0123.StoreUnsafe(ref firstBase, 0);
        first4567.StoreUnsafe(ref firstBase, 32);
        second0123.StoreUnsafe(ref secondBase, 0);
        second4567.StoreUnsafe(ref secondBase, 32);

        // The table row repeats in both halves, because both blocks use the same table.
        // The narrow operation keeps the lane order: the low 8 values come from the low 256 bits.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector256<short> Quantize(Vector512<float> row, Vector256<float> table)
        {
            Vector512<int> quantized = Vector512_.ConvertToInt32RoundToEven(row * Vector512.Create(table, table));
            return Vector256.NarrowWithSaturation(quantized.GetLower(), quantized.GetUpper());
        }
    }

    /// <summary>
    /// The <see cref="Vector256{Single}"/> version of the encoder stage.
    /// </summary>
    /// <param name="source">The first sample of the block.</param>
    /// <param name="stride">The distance between two source rows, in samples.</param>
    /// <param name="quantizationTable">The quantization table.</param>
    /// <param name="destination">The destination for the quantized coefficients.</param>
    private static void ForwardVector256(ref float source, nuint stride, ref Block8x8F quantizationTable, ref Block8x8 destination)
    {
        Vector256<float> shift = Vector256.Create(EncoderLevelShift);
        Vector256<float> v0 = Vector256.LoadUnsafe(ref source, 0) - shift;
        Vector256<float> v1 = Vector256.LoadUnsafe(ref source, stride) - shift;
        Vector256<float> v2 = Vector256.LoadUnsafe(ref source, 2 * stride) - shift;
        Vector256<float> v3 = Vector256.LoadUnsafe(ref source, 3 * stride) - shift;
        Vector256<float> v4 = Vector256.LoadUnsafe(ref source, 4 * stride) - shift;
        Vector256<float> v5 = Vector256.LoadUnsafe(ref source, 5 * stride) - shift;
        Vector256<float> v6 = Vector256.LoadUnsafe(ref source, 6 * stride) - shift;
        Vector256<float> v7 = Vector256.LoadUnsafe(ref source, 7 * stride) - shift;

        Transform<ForwardTransformOperator>(ref v0, ref v1, ref v2, ref v3, ref v4, ref v5, ref v6, ref v7);

        // Each register of 16-bit values holds two quantized rows. The zig-zag operator uses this layout.
        Vector256<short> v01 = Vector256.Create(Quantize(v0, quantizationTable.V256_0), Quantize(v1, quantizationTable.V256_1));
        Vector256<short> v23 = Vector256.Create(Quantize(v2, quantizationTable.V256_2), Quantize(v3, quantizationTable.V256_3));
        Vector256<short> v45 = Vector256.Create(Quantize(v4, quantizationTable.V256_4), Quantize(v5, quantizationTable.V256_5));
        Vector256<short> v67 = Vector256.Create(Quantize(v6, quantizationTable.V256_6), Quantize(v7, quantizationTable.V256_7));

        ref short target = ref Unsafe.As<Block8x8, short>(ref destination);
        if (Vector512.IsHardwareAccelerated)
        {
            // This path also runs on AVX-512 hardware, for the last block of a row with an odd block count.
            // Two Vector512 registers hold the whole block. The Vector512 form of the zig-zag operator uses two permutes.
            Vector512<short> v0123 = Vector512.Create(v01, v23);
            Vector512<short> v4567 = Vector512.Create(v45, v67);
            ZigZag.TransposingZigZagOperator.Invoke(ref v0123, ref v4567);
            v0123.StoreUnsafe(ref target, 0);
            v4567.StoreUnsafe(ref target, 32);
            return;
        }

        ZigZag.TransposingZigZagOperator.Invoke(ref v01, ref v23, ref v45, ref v67);
        v01.StoreUnsafe(ref target, 0);
        v23.StoreUnsafe(ref target, 16);
        v45.StoreUnsafe(ref target, 32);
        v67.StoreUnsafe(ref target, 48);

        // The narrow operation keeps the lane order: values 0 to 3 come from the low half and values 4 to 7 from the high half.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector128<short> Quantize(Vector256<float> row, Vector256<float> table)
        {
            Vector256<int> quantized = Vector256_.ConvertToInt32RoundToEven(row * table);
            return Vector128.NarrowWithSaturation(quantized.GetLower(), quantized.GetUpper());
        }
    }

    /// <summary>
    /// The <see cref="Vector128{Single}"/> version of the encoder stage.
    /// </summary>
    /// <remarks>
    /// Each row uses two registers: <c>left</c> holds columns 0 to 3 and <c>right</c> holds columns 4 to 7.
    /// </remarks>
    /// <param name="source">The first sample of the block.</param>
    /// <param name="stride">The distance between two source rows, in samples.</param>
    /// <param name="quantizationTable">The quantization table.</param>
    /// <param name="destination">The destination for the quantized coefficients.</param>
    private static void ForwardVector128(ref float source, nuint stride, ref Block8x8F quantizationTable, ref Block8x8 destination)
    {
        Vector128<float> shift = Vector128.Create(EncoderLevelShift);
        Vector128<float> l0 = Vector128.LoadUnsafe(ref source, 0) - shift;
        Vector128<float> r0 = Vector128.LoadUnsafe(ref source, 4) - shift;
        Vector128<float> l1 = Vector128.LoadUnsafe(ref source, stride) - shift;
        Vector128<float> r1 = Vector128.LoadUnsafe(ref source, stride + 4) - shift;
        Vector128<float> l2 = Vector128.LoadUnsafe(ref source, 2 * stride) - shift;
        Vector128<float> r2 = Vector128.LoadUnsafe(ref source, (2 * stride) + 4) - shift;
        Vector128<float> l3 = Vector128.LoadUnsafe(ref source, 3 * stride) - shift;
        Vector128<float> r3 = Vector128.LoadUnsafe(ref source, (3 * stride) + 4) - shift;
        Vector128<float> l4 = Vector128.LoadUnsafe(ref source, 4 * stride) - shift;
        Vector128<float> r4 = Vector128.LoadUnsafe(ref source, (4 * stride) + 4) - shift;
        Vector128<float> l5 = Vector128.LoadUnsafe(ref source, 5 * stride) - shift;
        Vector128<float> r5 = Vector128.LoadUnsafe(ref source, (5 * stride) + 4) - shift;
        Vector128<float> l6 = Vector128.LoadUnsafe(ref source, 6 * stride) - shift;
        Vector128<float> r6 = Vector128.LoadUnsafe(ref source, (6 * stride) + 4) - shift;
        Vector128<float> l7 = Vector128.LoadUnsafe(ref source, 7 * stride) - shift;
        Vector128<float> r7 = Vector128.LoadUnsafe(ref source, (7 * stride) + 4) - shift;

        Transform<ForwardTransformOperator>(
            ref l0,
            ref l1,
            ref l2,
            ref l3,
            ref l4,
            ref l5,
            ref l6,
            ref l7,
            ref r0,
            ref r1,
            ref r2,
            ref r3,
            ref r4,
            ref r5,
            ref r6,
            ref r7);

        // Each register of 16-bit values holds one quantized row. The zig-zag operator uses this layout.
        ref float tableBase = ref Unsafe.As<Block8x8F, float>(ref quantizationTable);
        Vector128<short> v0 = Quantize(l0, r0, ref tableBase, 0);
        Vector128<short> v1 = Quantize(l1, r1, ref tableBase, 8);
        Vector128<short> v2 = Quantize(l2, r2, ref tableBase, 16);
        Vector128<short> v3 = Quantize(l3, r3, ref tableBase, 24);
        Vector128<short> v4 = Quantize(l4, r4, ref tableBase, 32);
        Vector128<short> v5 = Quantize(l5, r5, ref tableBase, 40);
        Vector128<short> v6 = Quantize(l6, r6, ref tableBase, 48);
        Vector128<short> v7 = Quantize(l7, r7, ref tableBase, 56);

        ZigZag.TransposingZigZagOperator.Invoke(ref v0, ref v1, ref v2, ref v3, ref v4, ref v5, ref v6, ref v7);

        ref short target = ref Unsafe.As<Block8x8, short>(ref destination);
        v0.StoreUnsafe(ref target, 0);
        v1.StoreUnsafe(ref target, 8);
        v2.StoreUnsafe(ref target, 16);
        v3.StoreUnsafe(ref target, 24);
        v4.StoreUnsafe(ref target, 32);
        v5.StoreUnsafe(ref target, 40);
        v6.StoreUnsafe(ref target, 48);
        v7.StoreUnsafe(ref target, 56);

        // The table row starts at index. The narrow operation keeps the lane order: left gives values 0 to 3 and right gives values 4 to 7.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector128<short> Quantize(Vector128<float> left, Vector128<float> right, ref float tableBase, nuint index)
        {
            Vector128<int> low = Vector128_.ConvertToInt32RoundToEven(left * Vector128.LoadUnsafe(ref tableBase, index));
            Vector128<int> high = Vector128_.ConvertToInt32RoundToEven(right * Vector128.LoadUnsafe(ref tableBase, index + 4));
            return Vector128.NarrowWithSaturation(low, high);
        }
    }

    /// <summary>
    /// The scalar version of the encoder stage.
    /// </summary>
    /// <param name="source">The first sample of the block.</param>
    /// <param name="stride">The distance between two source rows, in samples.</param>
    /// <param name="quantizationTable">The quantization table.</param>
    /// <param name="destination">The destination for the quantized coefficients.</param>
    private static void ForwardScalar(ref float source, nuint stride, ref Block8x8F quantizationTable, ref Block8x8 destination)
    {
        Unsafe.SkipInit(out Block8x8F workspace);
        for (int row = 0; row < 8; row++)
        {
            ref float origin = ref Unsafe.Add(ref source, (uint)row * stride);
            for (int column = 0; column < 8; column++)
            {
                workspace[(row * 8) + column] = Unsafe.Add(ref origin, (uint)column) - EncoderLevelShift;
            }
        }

        Transform<ForwardTransformOperator>(ref workspace);

        // This loop rounds as the SIMD forms do: to the nearest integer, with ties to the even integer. Then it saturates to 16 bits.
        for (int i = 0; i < Block8x8F.Size; i++)
        {
            float quantized = MathF.Round(workspace[i] * quantizationTable[i]);
            destination[i] = (short)Math.Clamp(quantized, short.MinValue, short.MaxValue);
        }

        ZigZag.TransposingZigZagOperator.Invoke(ref destination);
    }
}
