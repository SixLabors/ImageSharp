// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

/// <content>
/// Contains the two-dimensional transform for each SIMD width.
/// </content>
internal static partial class FloatingPointDCT
{
    // The two-dimensional transform is one operator pass, a transpose and a second operator pass.
    // The first pass transforms the columns, because each lane is one column.
    // After the transpose, each lane is one row of the input. Thus, the second pass transforms the rows.
    // The result is transposed relative to the input:
    // - The decoder input is in transposed order. Thus, the IDCT output is in natural order.
    // - The encoder input is in natural order. Thus, the FDCT output is in transposed order.
    //   The transposing zig-zag operator after quantization puts these coefficients in the standard order.
    // The methods request aggressive inlining. When the JIT inlines them, the rows stay in registers between the passes.

    /// <summary>
    /// Applies the two-dimensional transform to one block in memory, one column at a time.
    /// </summary>
    /// <typeparam name="TOperator">The one-dimensional transform.</typeparam>
    /// <param name="block">The block.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Transform<TOperator>(ref Block8x8F block)
        where TOperator : struct, ITransformOperator
    {
        TransformColumns<TOperator>(ref block);
        block.TransposeInPlace();
        TransformColumns<TOperator>(ref block);
    }

    /// <summary>
    /// Applies the one-dimensional transform to each column of one block in memory.
    /// </summary>
    /// <typeparam name="TOperator">The one-dimensional transform.</typeparam>
    /// <param name="block">The block.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TransformColumns<TOperator>(ref Block8x8F block)
        where TOperator : struct, ITransformOperator
    {
        ref float origin = ref Unsafe.As<Block8x8F, float>(ref block);
        for (nuint column = 0; column < 8; column++)
        {
            ref float top = ref Unsafe.Add(ref origin, column);
            TOperator.Invoke(
                ref top,
                ref Unsafe.Add(ref top, 8),
                ref Unsafe.Add(ref top, 16),
                ref Unsafe.Add(ref top, 24),
                ref Unsafe.Add(ref top, 32),
                ref Unsafe.Add(ref top, 40),
                ref Unsafe.Add(ref top, 48),
                ref Unsafe.Add(ref top, 56));
        }
    }

    /// <summary>
    /// Applies the two-dimensional transform to one block in registers. Each row uses two registers.
    /// </summary>
    /// <typeparam name="TOperator">The one-dimensional transform.</typeparam>
    /// <param name="left0">Row 0, columns 0 to 3.</param>
    /// <param name="left1">Row 1, columns 0 to 3.</param>
    /// <param name="left2">Row 2, columns 0 to 3.</param>
    /// <param name="left3">Row 3, columns 0 to 3.</param>
    /// <param name="left4">Row 4, columns 0 to 3.</param>
    /// <param name="left5">Row 5, columns 0 to 3.</param>
    /// <param name="left6">Row 6, columns 0 to 3.</param>
    /// <param name="left7">Row 7, columns 0 to 3.</param>
    /// <param name="right0">Row 0, columns 4 to 7.</param>
    /// <param name="right1">Row 1, columns 4 to 7.</param>
    /// <param name="right2">Row 2, columns 4 to 7.</param>
    /// <param name="right3">Row 3, columns 4 to 7.</param>
    /// <param name="right4">Row 4, columns 4 to 7.</param>
    /// <param name="right5">Row 5, columns 4 to 7.</param>
    /// <param name="right6">Row 6, columns 4 to 7.</param>
    /// <param name="right7">Row 7, columns 4 to 7.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Transform<TOperator>(
        ref Vector128<float> left0,
        ref Vector128<float> left1,
        ref Vector128<float> left2,
        ref Vector128<float> left3,
        ref Vector128<float> left4,
        ref Vector128<float> left5,
        ref Vector128<float> left6,
        ref Vector128<float> left7,
        ref Vector128<float> right0,
        ref Vector128<float> right1,
        ref Vector128<float> right2,
        ref Vector128<float> right3,
        ref Vector128<float> right4,
        ref Vector128<float> right5,
        ref Vector128<float> right6,
        ref Vector128<float> right7)
        where TOperator : struct, ITransformOperator
    {
        TOperator.Invoke(ref left0, ref left1, ref left2, ref left3, ref left4, ref left5, ref left6, ref left7);
        TOperator.Invoke(ref right0, ref right1, ref right2, ref right3, ref right4, ref right5, ref right6, ref right7);

        Block8x8F.TransposeOperator.Invoke(
            ref left0,
            ref left1,
            ref left2,
            ref left3,
            ref left4,
            ref left5,
            ref left6,
            ref left7,
            ref right0,
            ref right1,
            ref right2,
            ref right3,
            ref right4,
            ref right5,
            ref right6,
            ref right7);

        TOperator.Invoke(ref left0, ref left1, ref left2, ref left3, ref left4, ref left5, ref left6, ref left7);
        TOperator.Invoke(ref right0, ref right1, ref right2, ref right3, ref right4, ref right5, ref right6, ref right7);
    }

    /// <summary>
    /// Applies the two-dimensional transform to one block in registers. Each register holds one row.
    /// </summary>
    /// <typeparam name="TOperator">The one-dimensional transform.</typeparam>
    /// <param name="v0">Row 0.</param>
    /// <param name="v1">Row 1.</param>
    /// <param name="v2">Row 2.</param>
    /// <param name="v3">Row 3.</param>
    /// <param name="v4">Row 4.</param>
    /// <param name="v5">Row 5.</param>
    /// <param name="v6">Row 6.</param>
    /// <param name="v7">Row 7.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Transform<TOperator>(
        ref Vector256<float> v0,
        ref Vector256<float> v1,
        ref Vector256<float> v2,
        ref Vector256<float> v3,
        ref Vector256<float> v4,
        ref Vector256<float> v5,
        ref Vector256<float> v6,
        ref Vector256<float> v7)
        where TOperator : struct, ITransformOperator
    {
        TOperator.Invoke(ref v0, ref v1, ref v2, ref v3, ref v4, ref v5, ref v6, ref v7);
        Block8x8F.TransposeOperator.Invoke(ref v0, ref v1, ref v2, ref v3, ref v4, ref v5, ref v6, ref v7);
        TOperator.Invoke(ref v0, ref v1, ref v2, ref v3, ref v4, ref v5, ref v6, ref v7);
    }

    /// <summary>
    /// Applies the two-dimensional transform to two blocks in registers.
    /// Each register holds the same row of both blocks.
    /// </summary>
    /// <typeparam name="TOperator">The one-dimensional transform.</typeparam>
    /// <param name="v0">Row 0 of both blocks.</param>
    /// <param name="v1">Row 1 of both blocks.</param>
    /// <param name="v2">Row 2 of both blocks.</param>
    /// <param name="v3">Row 3 of both blocks.</param>
    /// <param name="v4">Row 4 of both blocks.</param>
    /// <param name="v5">Row 5 of both blocks.</param>
    /// <param name="v6">Row 6 of both blocks.</param>
    /// <param name="v7">Row 7 of both blocks.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Transform<TOperator>(
        ref Vector512<float> v0,
        ref Vector512<float> v1,
        ref Vector512<float> v2,
        ref Vector512<float> v3,
        ref Vector512<float> v4,
        ref Vector512<float> v5,
        ref Vector512<float> v6,
        ref Vector512<float> v7)
        where TOperator : struct, ITransformOperator
    {
        TOperator.Invoke(ref v0, ref v1, ref v2, ref v3, ref v4, ref v5, ref v6, ref v7);
        Block8x8F.TransposeOperator.Invoke(ref v0, ref v1, ref v2, ref v3, ref v4, ref v5, ref v6, ref v7);
        TOperator.Invoke(ref v0, ref v1, ref v2, ref v3, ref v4, ref v5, ref v6, ref v7);
    }
}
