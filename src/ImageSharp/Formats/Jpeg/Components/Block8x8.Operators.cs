// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

/// <content>
/// Contains the block operator contract and the traversal that applies it.
/// </content>
internal partial struct Block8x8
{
    /// <summary>
    /// Defines an operation on all 64 values of a block, for scalar values and for each SIMD width.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A block has 64 16-bit values, 8 in each row. The register forms hold the rows in order:
    /// </para>
    /// <list type="bullet">
    /// <item><see cref="Vector128{T}"/>: one row in each register.</item>
    /// <item><see cref="Vector256{T}"/>: two rows in each register.</item>
    /// <item><see cref="Vector512{T}"/>: four rows in each register.</item>
    /// </list>
    /// <para>
    /// The scalar form works on the block in memory. All forms give the same result.
    /// </para>
    /// </remarks>
    internal interface IBlockOperator
    {
        /// <summary>
        /// Applies the operation to one block in memory.
        /// </summary>
        /// <param name="block">The block.</param>
        static abstract void Invoke(ref Block8x8 block);

        /// <summary>
        /// Applies the operation to one block in registers. Each register holds one row.
        /// </summary>
        /// <param name="v0">Row 0.</param>
        /// <param name="v1">Row 1.</param>
        /// <param name="v2">Row 2.</param>
        /// <param name="v3">Row 3.</param>
        /// <param name="v4">Row 4.</param>
        /// <param name="v5">Row 5.</param>
        /// <param name="v6">Row 6.</param>
        /// <param name="v7">Row 7.</param>
        static abstract void Invoke(
            ref Vector128<short> v0,
            ref Vector128<short> v1,
            ref Vector128<short> v2,
            ref Vector128<short> v3,
            ref Vector128<short> v4,
            ref Vector128<short> v5,
            ref Vector128<short> v6,
            ref Vector128<short> v7);

        /// <summary>
        /// Applies the operation to one block in registers. Each register holds two rows.
        /// </summary>
        /// <param name="v01">Rows 0 and 1.</param>
        /// <param name="v23">Rows 2 and 3.</param>
        /// <param name="v45">Rows 4 and 5.</param>
        /// <param name="v67">Rows 6 and 7.</param>
        static abstract void Invoke(ref Vector256<short> v01, ref Vector256<short> v23, ref Vector256<short> v45, ref Vector256<short> v67);

        /// <summary>
        /// Applies the operation to one block in registers. Each register holds four rows.
        /// </summary>
        /// <param name="v0123">Rows 0 to 3.</param>
        /// <param name="v4567">Rows 4 to 7.</param>
        static abstract void Invoke(ref Vector512<short> v0123, ref Vector512<short> v4567);
    }

    /// <summary>
    /// Applies <typeparamref name="TOperator"/> to one block with the widest supported width.
    /// </summary>
    /// <remarks>
    /// Callers that hold the rows in registers call the operator directly.
    /// </remarks>
    /// <typeparam name="TOperator">The operation.</typeparam>
    /// <param name="block">The block.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Apply<TOperator>(ref Block8x8 block)
        where TOperator : struct, IBlockOperator
    {
        ref short origin = ref Unsafe.As<Block8x8, short>(ref block);

        // The block is 128 bytes: two Vector512, four Vector256 or eight Vector128 registers. No width has a remainder.
        if (Vector512.IsHardwareAccelerated)
        {
            Vector512<short> v0123 = Vector512.LoadUnsafe(ref origin, 0);
            Vector512<short> v4567 = Vector512.LoadUnsafe(ref origin, 32);

            TOperator.Invoke(ref v0123, ref v4567);

            v0123.StoreUnsafe(ref origin, 0);
            v4567.StoreUnsafe(ref origin, 32);
        }
        else if (Vector256.IsHardwareAccelerated)
        {
            Vector256<short> v01 = Vector256.LoadUnsafe(ref origin, 0);
            Vector256<short> v23 = Vector256.LoadUnsafe(ref origin, 16);
            Vector256<short> v45 = Vector256.LoadUnsafe(ref origin, 32);
            Vector256<short> v67 = Vector256.LoadUnsafe(ref origin, 48);

            TOperator.Invoke(ref v01, ref v23, ref v45, ref v67);

            v01.StoreUnsafe(ref origin, 0);
            v23.StoreUnsafe(ref origin, 16);
            v45.StoreUnsafe(ref origin, 32);
            v67.StoreUnsafe(ref origin, 48);
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            Vector128<short> v0 = Vector128.LoadUnsafe(ref origin, 0);
            Vector128<short> v1 = Vector128.LoadUnsafe(ref origin, 8);
            Vector128<short> v2 = Vector128.LoadUnsafe(ref origin, 16);
            Vector128<short> v3 = Vector128.LoadUnsafe(ref origin, 24);
            Vector128<short> v4 = Vector128.LoadUnsafe(ref origin, 32);
            Vector128<short> v5 = Vector128.LoadUnsafe(ref origin, 40);
            Vector128<short> v6 = Vector128.LoadUnsafe(ref origin, 48);
            Vector128<short> v7 = Vector128.LoadUnsafe(ref origin, 56);

            TOperator.Invoke(ref v0, ref v1, ref v2, ref v3, ref v4, ref v5, ref v6, ref v7);

            v0.StoreUnsafe(ref origin, 0);
            v1.StoreUnsafe(ref origin, 8);
            v2.StoreUnsafe(ref origin, 16);
            v3.StoreUnsafe(ref origin, 24);
            v4.StoreUnsafe(ref origin, 32);
            v5.StoreUnsafe(ref origin, 40);
            v6.StoreUnsafe(ref origin, 48);
            v7.StoreUnsafe(ref origin, 56);
        }
        else
        {
            TOperator.Invoke(ref block);
        }
    }
}
