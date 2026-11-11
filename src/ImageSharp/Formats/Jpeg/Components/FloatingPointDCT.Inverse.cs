// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

/// <content>
/// Contains the decoder stage: dequantization, inverse transform, sample normalization and upsampling.
/// </content>
internal static partial class FloatingPointDCT
{
    // Each width does the complete stage in registers:
    // 1. The code loads the 16-bit coefficients, widens them to float and multiplies them by the dequantization table.
    // 2. The code applies the two-dimensional inverse transform of the operator.
    // 3. The code adds the level shift and clamps to [0, maximum].
    // 4. The code stores each output row. A subsampled component repeats each sample H times horizontally and each row V times.
    // The level shift is ceiling(maximum / 2), the center of the sample range.
    // The samples keep their fraction. The color conversion does the only rounding.
    //
    // The operator gives N x N samples, with N = TOperator.OutputSize (8, 4, 2 or 1).
    // After the transform, output row r is in the first N columns of row r.
    // The store operators write only the rows and columns with an index less than N.
    // Block i writes columns N * H * i to N * H * (i + 1) - 1 of N * V destination rows.
    // OutputSize and both scale factors are constants of the operator structs.
    // Thus, the JIT removes the code for the other sizes and factors. Each row store is at a constant multiple of the stride.
    //
    // Each full transform method is a separate JIT root. Thus, the JIT does not inline it into the row loop.
    // The JIT limits the inline work of each root to a multiple of the IL size of that root.
    // A full transform with its store operators is much larger than the row loop. When the loop inlines it,
    // the loop uses all of its budget, and the small operator calls in the transform stay as calls.
    // As a separate root, each transform has its own budget, so the JIT inlines all of its operators.
    // One call for each block or pair of blocks costs little compared to the transform.
    // The DC-only path is small, and many blocks use it. Thus, the row loop inlines it.

    /// <summary>
    /// Dequantizes one block, applies the inverse transform and writes the normalized samples.
    /// </summary>
    /// <param name="source">The quantized coefficients in transposed order.</param>
    /// <param name="dequantizationTable">The dequantization table, adjusted by <see cref="AdjustToIDCT"/>.</param>
    /// <param name="maximum">The maximum sample value.</param>
    /// <param name="destination">The first sample of the destination. The method writes 8 rows of 8 samples.</param>
    /// <param name="destinationStride">The distance between two destination rows, in samples.</param>
    public static void TransformIDCT(
        ref Block8x8 source,
        ref Block8x8F dequantizationTable,
        float maximum,
        ref float destination,
        int destinationStride)
        => TransformIDCT<InverseTransformOperator>(ref source, ref dequantizationTable, maximum, ref destination, destinationStride);

    /// <summary>
    /// Dequantizes a row of blocks, applies the inverse transform and writes the normalized samples.
    /// </summary>
    /// <remarks>
    /// Block <c>i</c> writes columns <c>8 * H * i</c> to <c>8 * H * (i + 1) - 1</c> of <c>8 * V</c> destination rows,
    /// H and V are the scale factors. Each sample covers H x V destination samples.
    /// </remarks>
    /// <param name="source">The quantized coefficient blocks in transposed order.</param>
    /// <param name="dequantizationTable">The dequantization table, adjusted by <see cref="AdjustToIDCT"/>.</param>
    /// <param name="maximum">The maximum sample value.</param>
    /// <param name="destination">The first sample of the destination.</param>
    /// <param name="destinationStride">The distance between two destination rows, in samples.</param>
    /// <param name="horizontalScale">The number of destination columns for each sample: 1 to 4.</param>
    /// <param name="verticalScale">The number of destination rows for each sample: 1 to 4.</param>
    /// <param name="clearSource">
    /// <see langword="true"/> to clear each source block after the method reads it. <see langword="false"/> to keep the source blocks.
    /// </param>
    public static void TransformIDCT(
        Span<Block8x8> source,
        ref Block8x8F dequantizationTable,
        float maximum,
        ref float destination,
        int destinationStride,
        int horizontalScale,
        int verticalScale,
        bool clearSource)
        => TransformIDCT<InverseTransformOperator>(
            source,
            ref dequantizationTable,
            maximum,
            ref destination,
            destinationStride,
            horizontalScale,
            verticalScale,
            clearSource);

    /// <summary>
    /// Dequantizes one block, applies the inverse transform of <typeparamref name="TOperator"/>
    /// and writes the normalized samples.
    /// </summary>
    /// <typeparam name="TOperator">The one-dimensional inverse transform.</typeparam>
    /// <param name="source">The quantized coefficients in transposed order.</param>
    /// <param name="dequantizationTable">The dequantization table, adjusted for the operator.</param>
    /// <param name="maximum">The maximum sample value.</param>
    /// <param name="destination">
    /// The first sample of the destination. The method writes N rows of N samples. N is the output size of the operator.
    /// </param>
    /// <param name="destinationStride">The distance between two destination rows, in samples.</param>
    public static void TransformIDCT<TOperator>(
        ref Block8x8 source,
        ref Block8x8F dequantizationTable,
        float maximum,
        ref float destination,
        int destinationStride)
        where TOperator : struct, ITransformOperator
        => TransformRow<TOperator, HorizontalScale1, VerticalScale1>(
            new Span<Block8x8>(ref source),
            ref dequantizationTable,
            maximum,
            ref destination,
            (uint)destinationStride,
            false);

    /// <summary>
    /// Dequantizes a row of blocks, applies the inverse transform of <typeparamref name="TOperator"/>
    /// and writes the normalized samples.
    /// </summary>
    /// <remarks>
    /// Block <c>i</c> writes columns <c>N * H * i</c> to <c>N * H * (i + 1) - 1</c> of <c>N * V</c> destination rows,
    /// N is the output size of the operator. H and V are the scale factors. Each sample covers H x V destination samples.
    /// The result is the same as one call for each block.
    /// </remarks>
    /// <typeparam name="TOperator">The one-dimensional inverse transform.</typeparam>
    /// <param name="source">The quantized coefficient blocks in transposed order.</param>
    /// <param name="dequantizationTable">The dequantization table, adjusted for the operator.</param>
    /// <param name="maximum">The maximum sample value.</param>
    /// <param name="destination">The first sample of the destination.</param>
    /// <param name="destinationStride">The distance between two destination rows, in samples.</param>
    /// <param name="horizontalScale">The number of destination columns for each sample: 1 to 4.</param>
    /// <param name="verticalScale">The number of destination rows for each sample: 1 to 4.</param>
    /// <param name="clearSource">
    /// <see langword="true"/> to clear each source block after the method reads it. <see langword="false"/> to keep the source blocks.
    /// A baseline decoder decodes the next MCU row into the same blocks, and its Huffman decoder writes the DC coefficient and only the non-zero AC coefficients.
    /// The method clears each block while the block is in the L1 cache, so the decoder needs no separate clear pass.
    /// </param>
    public static void TransformIDCT<TOperator>(
        Span<Block8x8> source,
        ref Block8x8F dequantizationTable,
        float maximum,
        ref float destination,
        int destinationStride,
        int horizontalScale,
        int verticalScale,
        bool clearSource)
        where TOperator : struct, ITransformOperator
    {
        // The frame header limits each sampling factor to 1 to 4. Thus, the integer divisors are 1, 2, 3 or 4,
        // and each factor pair has operators with constant shuffles and constant row offsets.
        nuint stride = (uint)destinationStride;
        switch (horizontalScale)
        {
            case 1:
                TransformRow<TOperator, HorizontalScale1>(source, ref dequantizationTable, maximum, ref destination, stride, verticalScale, clearSource);
                break;
            case 2:
                TransformRow<TOperator, HorizontalScale2>(source, ref dequantizationTable, maximum, ref destination, stride, verticalScale, clearSource);
                break;
            case 3:
                TransformRow<TOperator, HorizontalScale3>(source, ref dequantizationTable, maximum, ref destination, stride, verticalScale, clearSource);
                break;
            default:
                TransformRow<TOperator, HorizontalScale4>(source, ref dequantizationTable, maximum, ref destination, stride, verticalScale, clearSource);
                break;
        }
    }

    /// <summary>
    /// Selects the vertical scale operator for a row of blocks.
    /// </summary>
    /// <typeparam name="TOperator">The one-dimensional inverse transform.</typeparam>
    /// <typeparam name="THorizontal">The horizontal scale operator.</typeparam>
    /// <param name="source">The quantized coefficient blocks.</param>
    /// <param name="dequantizationTable">The dequantization table.</param>
    /// <param name="maximum">The maximum sample value.</param>
    /// <param name="destination">The first sample of the destination.</param>
    /// <param name="stride">The distance between two destination rows, in samples.</param>
    /// <param name="verticalScale">The number of destination rows for each sample: 1 to 4.</param>
    /// <param name="clearSource">Whether to clear each source block after the method reads it.</param>
    private static void TransformRow<TOperator, THorizontal>(
        Span<Block8x8> source,
        ref Block8x8F dequantizationTable,
        float maximum,
        ref float destination,
        nuint stride,
        int verticalScale,
        bool clearSource)
        where TOperator : struct, ITransformOperator
        where THorizontal : struct, IHorizontalScaleOperator
    {
        switch (verticalScale)
        {
            case 1:
                TransformRow<TOperator, THorizontal, VerticalScale1>(source, ref dequantizationTable, maximum, ref destination, stride, clearSource);
                break;
            case 2:
                TransformRow<TOperator, THorizontal, VerticalScale2>(source, ref dequantizationTable, maximum, ref destination, stride, clearSource);
                break;
            case 3:
                TransformRow<TOperator, THorizontal, VerticalScale3>(source, ref dequantizationTable, maximum, ref destination, stride, clearSource);
                break;
            default:
                TransformRow<TOperator, THorizontal, VerticalScale4>(source, ref dequantizationTable, maximum, ref destination, stride, clearSource);
                break;
        }
    }

    /// <summary>
    /// Transforms a row of blocks. Two adjacent blocks that are not DC-only use one <see cref="Vector512{T}"/> transform
    /// when it is available.
    /// </summary>
    /// <typeparam name="TOperator">The one-dimensional inverse transform.</typeparam>
    /// <typeparam name="THorizontal">The horizontal scale operator.</typeparam>
    /// <typeparam name="TVertical">The vertical scale operator.</typeparam>
    /// <param name="source">The quantized coefficient blocks.</param>
    /// <param name="dequantizationTable">The dequantization table.</param>
    /// <param name="maximum">The maximum sample value.</param>
    /// <param name="destination">The first sample of the destination.</param>
    /// <param name="stride">The distance between two destination rows, in samples.</param>
    /// <param name="clearSource">Whether to clear each source block after the method reads it.</param>
    private static void TransformRow<TOperator, THorizontal, TVertical>(
        Span<Block8x8> source,
        ref Block8x8F dequantizationTable,
        float maximum,
        ref float destination,
        nuint stride,
        bool clearSource)
        where TOperator : struct, ITransformOperator
        where THorizontal : struct, IHorizontalScaleOperator
        where TVertical : struct, IVerticalScaleOperator
    {
        // Each block covers this number of destination columns.
        uint blockWidth = (uint)(TOperator.OutputSize * THorizontal.Factor);

        // Each stage has one call site. Thus, each instantiation has one copy of each stage, and the JIT can inline
        // the operators of each stage. The size, the factors and the hardware checks are constants, so only one
        // width remains for each instantiation.
        for (int i = 0; i < source.Length; i++)
        {
            ref Block8x8 block = ref source[i];
            ref float target = ref Unsafe.Add(ref destination, (uint)i * blockWidth);

            // The 8x8 to 1x1 transform uses only the DC coefficient. Thus, every block of that operator takes the DC path.
            if (TOperator.OutputSize == 1 || block.IsDcOnly())
            {
                WriteDcOnly<TOperator, THorizontal, TVertical>(ref block, ref dequantizationTable, maximum, ref target, stride);
            }
            else if (Vector512.IsHardwareAccelerated && (uint)(i + 1) < (uint)source.Length && !source[i + 1].IsDcOnly())
            {
                // The next block also needs the transform, so one set of registers transforms both blocks.
                // When the next block is DC-only, this block uses the single-block width,
                // and the next block takes the DC path in the next iteration.
                // Each block is independent. Thus, the use of one transform for two blocks does not change the samples.
                ref Block8x8 next = ref source[i + 1];
                InverseVector512<TOperator, THorizontal, TVertical>(ref block, ref next, ref dequantizationTable, maximum, ref target, stride);

                // The transform read the next block. Thus, the clear operation writes to the L1 cache.
                if (clearSource)
                {
                    next = default;
                }

                i++;
            }
            else if (Vector256.IsHardwareAccelerated)
            {
                InverseVector256<TOperator, THorizontal, TVertical>(ref block, ref dequantizationTable, maximum, ref target, stride);
            }
            else if (Vector128.IsHardwareAccelerated)
            {
                InverseVector128<TOperator, THorizontal, TVertical>(ref block, ref dequantizationTable, maximum, ref target, stride);
            }
            else
            {
                InverseScalar<TOperator, THorizontal, TVertical>(ref block, ref dequantizationTable, maximum, ref target, stride);
            }

            // The stage read the block. Thus, the clear operation writes to the L1 cache.
            if (clearSource)
            {
                block = default;
            }
        }
    }

    /// <summary>
    /// Writes one block that has only a DC coefficient.
    /// </summary>
    /// <remarks>
    /// In both passes of each inverse transform, the DC term goes to every output with no other non-zero input.
    /// The full transform adds and subtracts zero terms. The scaled transforms multiply by 2 or 4,
    /// then by 0.5 or 0.25. These operations are exact. Thus, all outputs are the dequantized DC,
    /// and this shortcut gives the same samples as the transform.
    /// </remarks>
    /// <typeparam name="TOperator">The one-dimensional inverse transform.</typeparam>
    /// <typeparam name="THorizontal">The horizontal scale operator.</typeparam>
    /// <typeparam name="TVertical">The vertical scale operator.</typeparam>
    /// <param name="source">The quantized coefficients.</param>
    /// <param name="dequantizationTable">The dequantization table.</param>
    /// <param name="maximum">The maximum sample value.</param>
    /// <param name="destination">The first sample of the destination.</param>
    /// <param name="stride">The distance between two destination rows, in samples.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteDcOnly<TOperator, THorizontal, TVertical>(
        ref Block8x8 source,
        ref Block8x8F dequantizationTable,
        float maximum,
        ref float destination,
        nuint stride)
        where TOperator : struct, ITransformOperator
        where THorizontal : struct, IHorizontalScaleOperator
        where TVertical : struct, IVerticalScaleOperator
    {
        // Every output sample of the block has the same value. Thus, each output row is the broadcast sample.
        // The store operators of the transform repeat this sample H times in each row. They write each row V times.
        // The size and both factors are constants. Thus, each store is at a constant multiple of the stride.
        // The SIMD paths need no fill loop for a row.
        int size = TOperator.OutputSize;
        nuint rowStep = (uint)TVertical.Factor * stride;
        float offset = MathF.Ceiling(maximum * .5F);
        float sample = Math.Clamp((source[0] * dequantizationTable[0]) + offset, 0F, maximum);

        if (size == 1)
        {
            THorizontal.Store<TVertical>(sample, ref destination, 0, stride);
        }
        else if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> row = Vector256.Create(sample);
            for (nuint r = 0; r < (uint)size; r++)
            {
                THorizontal.Store<TVertical>(row, size, ref destination, r * rowStep, stride);
            }
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            // One register holds four samples. Columns 4 to 7 of the full transform start after H * 4 destination samples.
            Vector128<float> row = Vector128.Create(sample);
            for (nuint r = 0; r < (uint)size; r++)
            {
                THorizontal.Store<TVertical>(row, Math.Min(size, 4), ref destination, r * rowStep, stride);
                if (size == 8)
                {
                    THorizontal.Store<TVertical>(row, 4, ref destination, (r * rowStep) + (4 * (uint)THorizontal.Factor), stride);
                }
            }
        }
        else
        {
            for (nuint r = 0; r < (uint)size; r++)
            {
                for (nuint c = 0; c < (uint)size; c++)
                {
                    THorizontal.Store<TVertical>(sample, ref destination, (r * rowStep) + (c * (uint)THorizontal.Factor), stride);
                }
            }
        }
    }

    /// <summary>
    /// The <see cref="Vector512{Single}"/> version of the decoder stage for two blocks.
    /// </summary>
    /// <remarks>
    /// The low 256 bits of each register hold a row of <paramref name="first"/>.
    /// The high 256 bits hold the same row of <paramref name="second"/>.
    /// The output of <paramref name="second"/> follows the output of <paramref name="first"/> in each destination row.
    /// </remarks>
    /// <typeparam name="TOperator">The one-dimensional inverse transform.</typeparam>
    /// <typeparam name="THorizontal">The horizontal scale operator.</typeparam>
    /// <typeparam name="TVertical">The vertical scale operator.</typeparam>
    /// <param name="first">The quantized coefficients of the first block.</param>
    /// <param name="second">The quantized coefficients of the second block.</param>
    /// <param name="dequantizationTable">The dequantization table.</param>
    /// <param name="maximum">The maximum sample value.</param>
    /// <param name="destination">The first sample of the first block.</param>
    /// <param name="stride">The distance between two destination rows, in samples.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InverseVector512<TOperator, THorizontal, TVertical>(
        ref Block8x8 first,
        ref Block8x8 second,
        ref Block8x8F dequantizationTable,
        float maximum,
        ref float destination,
        nuint stride)
        where TOperator : struct, ITransformOperator
        where THorizontal : struct, IHorizontalScaleOperator
        where TVertical : struct, IVerticalScaleOperator
    {
        ref short firstBase = ref Unsafe.As<Block8x8, short>(ref first);
        ref short secondBase = ref Unsafe.As<Block8x8, short>(ref second);

        // Both blocks use the same table. Thus, the table row repeats in both halves.
        Vector512<float> v0 = LoadRowPair(ref firstBase, ref secondBase, 0) * Vector512.Create(dequantizationTable.V256_0, dequantizationTable.V256_0);
        Vector512<float> v1 = LoadRowPair(ref firstBase, ref secondBase, 1) * Vector512.Create(dequantizationTable.V256_1, dequantizationTable.V256_1);
        Vector512<float> v2 = LoadRowPair(ref firstBase, ref secondBase, 2) * Vector512.Create(dequantizationTable.V256_2, dequantizationTable.V256_2);
        Vector512<float> v3 = LoadRowPair(ref firstBase, ref secondBase, 3) * Vector512.Create(dequantizationTable.V256_3, dequantizationTable.V256_3);
        Vector512<float> v4 = LoadRowPair(ref firstBase, ref secondBase, 4) * Vector512.Create(dequantizationTable.V256_4, dequantizationTable.V256_4);
        Vector512<float> v5 = LoadRowPair(ref firstBase, ref secondBase, 5) * Vector512.Create(dequantizationTable.V256_5, dequantizationTable.V256_5);
        Vector512<float> v6 = LoadRowPair(ref firstBase, ref secondBase, 6) * Vector512.Create(dequantizationTable.V256_6, dequantizationTable.V256_6);
        Vector512<float> v7 = LoadRowPair(ref firstBase, ref secondBase, 7) * Vector512.Create(dequantizationTable.V256_7, dequantizationTable.V256_7);

        Transform<TOperator>(ref v0, ref v1, ref v2, ref v3, ref v4, ref v5, ref v6, ref v7);

        // The code normalizes each output row and then stores it. It does not store the rows at or after the output size.
        Vector512<float> max = Vector512.Create(maximum);
        Vector512<float> offset = Vector512.Ceiling(max * .5F);
        int size = TOperator.OutputSize;

        StoreRowPair<THorizontal, TVertical>(Vector512.Clamp(v0 + offset, Vector512<float>.Zero, max), 0, size, ref destination, stride);
        StoreRowPair<THorizontal, TVertical>(Vector512.Clamp(v1 + offset, Vector512<float>.Zero, max), 1, size, ref destination, stride);
        if (size > 2)
        {
            StoreRowPair<THorizontal, TVertical>(Vector512.Clamp(v2 + offset, Vector512<float>.Zero, max), 2, size, ref destination, stride);
            StoreRowPair<THorizontal, TVertical>(Vector512.Clamp(v3 + offset, Vector512<float>.Zero, max), 3, size, ref destination, stride);
        }

        if (size > 4)
        {
            StoreRowPair<THorizontal, TVertical>(Vector512.Clamp(v4 + offset, Vector512<float>.Zero, max), 4, size, ref destination, stride);
            StoreRowPair<THorizontal, TVertical>(Vector512.Clamp(v5 + offset, Vector512<float>.Zero, max), 5, size, ref destination, stride);
            StoreRowPair<THorizontal, TVertical>(Vector512.Clamp(v6 + offset, Vector512<float>.Zero, max), 6, size, ref destination, stride);
            StoreRowPair<THorizontal, TVertical>(Vector512.Clamp(v7 + offset, Vector512<float>.Zero, max), 7, size, ref destination, stride);
        }

        // Row r of both blocks: the 8 coefficients of the first block, then the 8 coefficients of the second block.
        // One widen operation gives 16 32-bit values in the same order. The widen operation and the conversion to float are exact.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector512<float> LoadRowPair(ref short firstBase, ref short secondBase, nuint row)
        {
            Vector256<short> pair = Vector256.Create(
                Vector128.LoadUnsafe(ref firstBase, row * 8),
                Vector128.LoadUnsafe(ref secondBase, row * 8));

            return Vector512.ConvertToSingle(Vector512.WidenLower(pair.ToVector512Unsafe()));
        }
    }

    /// <summary>
    /// The <see cref="Vector256{Single}"/> version of the decoder stage.
    /// </summary>
    /// <typeparam name="TOperator">The one-dimensional inverse transform.</typeparam>
    /// <typeparam name="THorizontal">The horizontal scale operator.</typeparam>
    /// <typeparam name="TVertical">The vertical scale operator.</typeparam>
    /// <param name="source">The quantized coefficients.</param>
    /// <param name="dequantizationTable">The dequantization table.</param>
    /// <param name="maximum">The maximum sample value.</param>
    /// <param name="destination">The first sample of the destination.</param>
    /// <param name="stride">The distance between two destination rows, in samples.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InverseVector256<TOperator, THorizontal, TVertical>(
        ref Block8x8 source,
        ref Block8x8F dequantizationTable,
        float maximum,
        ref float destination,
        nuint stride)
        where TOperator : struct, ITransformOperator
        where THorizontal : struct, IHorizontalScaleOperator
        where TVertical : struct, IVerticalScaleOperator
    {
        ref short sourceBase = ref Unsafe.As<Block8x8, short>(ref source);

        Vector256<float> v0 = LoadRow(ref sourceBase, 0) * dequantizationTable.V256_0;
        Vector256<float> v1 = LoadRow(ref sourceBase, 1) * dequantizationTable.V256_1;
        Vector256<float> v2 = LoadRow(ref sourceBase, 2) * dequantizationTable.V256_2;
        Vector256<float> v3 = LoadRow(ref sourceBase, 3) * dequantizationTable.V256_3;
        Vector256<float> v4 = LoadRow(ref sourceBase, 4) * dequantizationTable.V256_4;
        Vector256<float> v5 = LoadRow(ref sourceBase, 5) * dequantizationTable.V256_5;
        Vector256<float> v6 = LoadRow(ref sourceBase, 6) * dequantizationTable.V256_6;
        Vector256<float> v7 = LoadRow(ref sourceBase, 7) * dequantizationTable.V256_7;

        Transform<TOperator>(ref v0, ref v1, ref v2, ref v3, ref v4, ref v5, ref v6, ref v7);

        // The code normalizes each output row and then stores it. It does not store the rows at or after the output size.
        Vector256<float> max = Vector256.Create(maximum);
        Vector256<float> offset = Vector256.Ceiling(max * .5F);
        int size = TOperator.OutputSize;

        StoreRow<THorizontal, TVertical>(Vector256.Clamp(v0 + offset, Vector256<float>.Zero, max), 0, size, ref destination, stride);
        StoreRow<THorizontal, TVertical>(Vector256.Clamp(v1 + offset, Vector256<float>.Zero, max), 1, size, ref destination, stride);
        if (size > 2)
        {
            StoreRow<THorizontal, TVertical>(Vector256.Clamp(v2 + offset, Vector256<float>.Zero, max), 2, size, ref destination, stride);
            StoreRow<THorizontal, TVertical>(Vector256.Clamp(v3 + offset, Vector256<float>.Zero, max), 3, size, ref destination, stride);
        }

        if (size > 4)
        {
            StoreRow<THorizontal, TVertical>(Vector256.Clamp(v4 + offset, Vector256<float>.Zero, max), 4, size, ref destination, stride);
            StoreRow<THorizontal, TVertical>(Vector256.Clamp(v5 + offset, Vector256<float>.Zero, max), 5, size, ref destination, stride);
            StoreRow<THorizontal, TVertical>(Vector256.Clamp(v6 + offset, Vector256<float>.Zero, max), 6, size, ref destination, stride);
            StoreRow<THorizontal, TVertical>(Vector256.Clamp(v7 + offset, Vector256<float>.Zero, max), 7, size, ref destination, stride);
        }

        // Row r has eight 16-bit coefficients. The widen operation to 32 bits and the conversion to float are exact.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector256<float> LoadRow(ref short sourceBase, nuint row)
            => Vector256.ConvertToSingle(Vector256_.Widen(Vector128.LoadUnsafe(ref sourceBase, row * 8)));
    }

    /// <summary>
    /// The <see cref="Vector128{Single}"/> version of the decoder stage.
    /// </summary>
    /// <remarks>
    /// Each row uses two registers: <c>left</c> holds columns 0 to 3 and <c>right</c> holds columns 4 to 7.
    /// </remarks>
    /// <typeparam name="TOperator">The one-dimensional inverse transform.</typeparam>
    /// <typeparam name="THorizontal">The horizontal scale operator.</typeparam>
    /// <typeparam name="TVertical">The vertical scale operator.</typeparam>
    /// <param name="source">The quantized coefficients.</param>
    /// <param name="dequantizationTable">The dequantization table.</param>
    /// <param name="maximum">The maximum sample value.</param>
    /// <param name="destination">The first sample of the destination.</param>
    /// <param name="stride">The distance between two destination rows, in samples.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InverseVector128<TOperator, THorizontal, TVertical>(
        ref Block8x8 source,
        ref Block8x8F dequantizationTable,
        float maximum,
        ref float destination,
        nuint stride)
        where TOperator : struct, ITransformOperator
        where THorizontal : struct, IHorizontalScaleOperator
        where TVertical : struct, IVerticalScaleOperator
    {
        ref short sourceBase = ref Unsafe.As<Block8x8, short>(ref source);
        ref float tableBase = ref Unsafe.As<Block8x8F, float>(ref dequantizationTable);

        LoadRow(ref sourceBase, ref tableBase, 0, out Vector128<float> l0, out Vector128<float> r0);
        LoadRow(ref sourceBase, ref tableBase, 1, out Vector128<float> l1, out Vector128<float> r1);
        LoadRow(ref sourceBase, ref tableBase, 2, out Vector128<float> l2, out Vector128<float> r2);
        LoadRow(ref sourceBase, ref tableBase, 3, out Vector128<float> l3, out Vector128<float> r3);
        LoadRow(ref sourceBase, ref tableBase, 4, out Vector128<float> l4, out Vector128<float> r4);
        LoadRow(ref sourceBase, ref tableBase, 5, out Vector128<float> l5, out Vector128<float> r5);
        LoadRow(ref sourceBase, ref tableBase, 6, out Vector128<float> l6, out Vector128<float> r6);
        LoadRow(ref sourceBase, ref tableBase, 7, out Vector128<float> l7, out Vector128<float> r7);

        Transform<TOperator>(
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

        // The code normalizes each output row and then stores it. It does not store the rows at or after the output size.
        Vector128<float> max = Vector128.Create(maximum);
        Vector128<float> offset = Vector128.Ceiling(max * .5F);
        int size = TOperator.OutputSize;

        StoreRow<THorizontal, TVertical>(l0, r0, 0, offset, max, size, ref destination, stride);
        StoreRow<THorizontal, TVertical>(l1, r1, 1, offset, max, size, ref destination, stride);
        if (size > 2)
        {
            StoreRow<THorizontal, TVertical>(l2, r2, 2, offset, max, size, ref destination, stride);
            StoreRow<THorizontal, TVertical>(l3, r3, 3, offset, max, size, ref destination, stride);
        }

        if (size > 4)
        {
            StoreRow<THorizontal, TVertical>(l4, r4, 4, offset, max, size, ref destination, stride);
            StoreRow<THorizontal, TVertical>(l5, r5, 5, offset, max, size, ref destination, stride);
            StoreRow<THorizontal, TVertical>(l6, r6, 6, offset, max, size, ref destination, stride);
            StoreRow<THorizontal, TVertical>(l7, r7, 7, offset, max, size, ref destination, stride);
        }

        // Row r has eight 16-bit coefficients. The low four go to left and the high four go to right.
        // The widen operation to 32 bits and the conversion to float are exact.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void LoadRow(ref short sourceBase, ref float tableBase, nuint row, out Vector128<float> left, out Vector128<float> right)
        {
            Vector128<short> coefficients = Vector128.LoadUnsafe(ref sourceBase, row * 8);
            left = Vector128.ConvertToSingle(Vector128.WidenLower(coefficients)) * Vector128.LoadUnsafe(ref tableBase, row * 8);
            right = Vector128.ConvertToSingle(Vector128.WidenUpper(coefficients)) * Vector128.LoadUnsafe(ref tableBase, (row * 8) + 4);
        }
    }

    /// <summary>
    /// The scalar version of the decoder stage.
    /// </summary>
    /// <typeparam name="TOperator">The one-dimensional inverse transform.</typeparam>
    /// <typeparam name="THorizontal">The horizontal scale operator.</typeparam>
    /// <typeparam name="TVertical">The vertical scale operator.</typeparam>
    /// <param name="source">The quantized coefficients.</param>
    /// <param name="dequantizationTable">The dequantization table.</param>
    /// <param name="maximum">The maximum sample value.</param>
    /// <param name="destination">The first sample of the destination.</param>
    /// <param name="stride">The distance between two destination rows, in samples.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void InverseScalar<TOperator, THorizontal, TVertical>(
        ref Block8x8 source,
        ref Block8x8F dequantizationTable,
        float maximum,
        ref float destination,
        nuint stride)
        where TOperator : struct, ITransformOperator
        where THorizontal : struct, IHorizontalScaleOperator
        where TVertical : struct, IVerticalScaleOperator
    {
        Unsafe.SkipInit(out Block8x8F workspace);
        for (int i = 0; i < Block8x8F.Size; i++)
        {
            workspace[i] = source[i] * dequantizationTable[i];
        }

        Transform<TOperator>(ref workspace);

        // Output row r goes to destination rows r * V to r * V + V - 1. Output column c goes to columns c * H to c * H + H - 1.
        int size = TOperator.OutputSize;
        uint factor = (uint)THorizontal.Factor;
        float offset = MathF.Ceiling(maximum * .5F);
        for (int row = 0; row < size; row++)
        {
            nuint index = (uint)(row * TVertical.Factor) * stride;
            for (int column = 0; column < size; column++)
            {
                float sample = Math.Clamp(workspace[(row * 8) + column] + offset, 0F, maximum);
                THorizontal.Store<TVertical>(sample, ref destination, index + ((uint)column * factor), stride);
            }
        }
    }

    /// <summary>
    /// Stores one normalized output row of two blocks.
    /// </summary>
    /// <remarks>
    /// Output row <c>r</c> goes to destination rows <c>r * V</c> to <c>r * V + V - 1</c>. Lanes 0 to <c>size - 1</c> hold
    /// the first block and lanes 8 to <c>8 + size - 1</c> hold the second block. The operator repeats the samples.
    /// </remarks>
    /// <typeparam name="THorizontal">The horizontal scale operator.</typeparam>
    /// <typeparam name="TVertical">The vertical scale operator.</typeparam>
    /// <param name="row">The normalized samples.</param>
    /// <param name="outputRow">The output row.</param>
    /// <param name="size">The output size of the transform.</param>
    /// <param name="destination">The first sample of the destination.</param>
    /// <param name="stride">The distance between two destination rows, in samples.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreRowPair<THorizontal, TVertical>(Vector512<float> row, nuint outputRow, int size, ref float destination, nuint stride)
        where THorizontal : struct, IHorizontalScaleOperator
        where TVertical : struct, IVerticalScaleOperator
        => THorizontal.Store<TVertical>(row, size, ref destination, outputRow * (uint)TVertical.Factor * stride, stride);

    /// <summary>
    /// Stores one normalized output row of one block.
    /// </summary>
    /// <remarks>
    /// Output row <c>r</c> goes to destination rows <c>r * V</c> to <c>r * V + V - 1</c>.
    /// Lanes 0 to <c>size - 1</c> hold the samples. The operator repeats the samples.
    /// </remarks>
    /// <typeparam name="THorizontal">The horizontal scale operator.</typeparam>
    /// <typeparam name="TVertical">The vertical scale operator.</typeparam>
    /// <param name="row">The normalized samples.</param>
    /// <param name="outputRow">The output row.</param>
    /// <param name="size">The output size of the transform.</param>
    /// <param name="destination">The first sample of the destination.</param>
    /// <param name="stride">The distance between two destination rows, in samples.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreRow<THorizontal, TVertical>(Vector256<float> row, nuint outputRow, int size, ref float destination, nuint stride)
        where THorizontal : struct, IHorizontalScaleOperator
        where TVertical : struct, IVerticalScaleOperator
        => THorizontal.Store<TVertical>(row, size, ref destination, outputRow * (uint)TVertical.Factor * stride, stride);

    /// <summary>
    /// Normalizes and stores one output row of one block that uses two registers for each row.
    /// </summary>
    /// <remarks>
    /// Output row <c>r</c> goes to destination rows <c>r * V</c> to <c>r * V + V - 1</c>. Left holds columns 0 to 3.
    /// Right holds columns 4 to 7, which only the full transform uses. The right samples start after the H * 4
    /// destination samples of the left samples.
    /// </remarks>
    /// <typeparam name="THorizontal">The horizontal scale operator.</typeparam>
    /// <typeparam name="TVertical">The vertical scale operator.</typeparam>
    /// <param name="left">Columns 0 to 3 of the output row, before normalization.</param>
    /// <param name="right">Columns 4 to 7 of the output row, before normalization.</param>
    /// <param name="outputRow">The output row.</param>
    /// <param name="offset">The level shift.</param>
    /// <param name="max">The maximum sample value.</param>
    /// <param name="size">The output size of the transform.</param>
    /// <param name="destination">The first sample of the destination.</param>
    /// <param name="stride">The distance between two destination rows, in samples.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreRow<THorizontal, TVertical>(
        Vector128<float> left,
        Vector128<float> right,
        nuint outputRow,
        Vector128<float> offset,
        Vector128<float> max,
        int size,
        ref float destination,
        nuint stride)
        where THorizontal : struct, IHorizontalScaleOperator
        where TVertical : struct, IVerticalScaleOperator
    {
        nuint index = outputRow * (uint)TVertical.Factor * stride;
        Vector128<float> leftSamples = Vector128.Clamp(left + offset, Vector128<float>.Zero, max);
        THorizontal.Store<TVertical>(leftSamples, Math.Min(size, 4), ref destination, index, stride);
        if (size == 8)
        {
            Vector128<float> rightSamples = Vector128.Clamp(right + offset, Vector128<float>.Zero, max);
            THorizontal.Store<TVertical>(rightSamples, 4, ref destination, index + (4 * (uint)THorizontal.Factor), stride);
        }
    }
}
