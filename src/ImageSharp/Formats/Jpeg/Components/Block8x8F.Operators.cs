// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;

namespace SixLabors.ImageSharp.Formats.Jpeg.Components;

/// <content>
/// Contains the block operators and the traversal that applies them.
/// </content>
internal partial struct Block8x8F
{
    /// <summary>
    /// Defines an operation on all 64 values of a block, for scalar values and for each SIMD width.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The register forms hold the block rows in registers:
    /// </para>
    /// <list type="bullet">
    /// <item><see cref="Vector128{T}"/>: two registers for each row. <c>left</c> holds columns 0 to 3 and <c>right</c> holds columns 4 to 7.</item>
    /// <item><see cref="Vector256{T}"/>: one register for each row.</item>
    /// <item>
    /// <see cref="Vector512{T}"/>: two blocks. The low 256 bits of a register hold a row of the first block.
    /// The high 256 bits hold the same row of the second block.
    /// </item>
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
        static abstract void Invoke(ref Block8x8F block);

        /// <summary>
        /// Applies the operation to one block in registers. Each row uses two registers.
        /// </summary>
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
        static abstract void Invoke(
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
            ref Vector128<float> right7);

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
            ref Vector256<float> v0,
            ref Vector256<float> v1,
            ref Vector256<float> v2,
            ref Vector256<float> v3,
            ref Vector256<float> v4,
            ref Vector256<float> v5,
            ref Vector256<float> v6,
            ref Vector256<float> v7);

        /// <summary>
        /// Applies the operation to two blocks in registers. Each register holds the same row of both blocks.
        /// </summary>
        /// <param name="v0">Row 0 of both blocks.</param>
        /// <param name="v1">Row 1 of both blocks.</param>
        /// <param name="v2">Row 2 of both blocks.</param>
        /// <param name="v3">Row 3 of both blocks.</param>
        /// <param name="v4">Row 4 of both blocks.</param>
        /// <param name="v5">Row 5 of both blocks.</param>
        /// <param name="v6">Row 6 of both blocks.</param>
        /// <param name="v7">Row 7 of both blocks.</param>
        static abstract void Invoke(
            ref Vector512<float> v0,
            ref Vector512<float> v1,
            ref Vector512<float> v2,
            ref Vector512<float> v3,
            ref Vector512<float> v4,
            ref Vector512<float> v5,
            ref Vector512<float> v6,
            ref Vector512<float> v7);
    }

    /// <summary>
    /// Applies <typeparamref name="TOperator"/> to one block with the widest supported width.
    /// </summary>
    /// <remarks>
    /// The <see cref="Vector512{T}"/> form needs two blocks. Thus, one block uses the <see cref="Vector256{T}"/> form when it is available.
    /// Callers that hold the rows in registers call the operator directly.
    /// </remarks>
    /// <typeparam name="TOperator">The operation.</typeparam>
    /// <param name="block">The block.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Apply<TOperator>(ref Block8x8F block)
        where TOperator : struct, IBlockOperator
    {
        // One block is 64 floats: eight Vector256 rows or sixteen Vector128 half rows. No width has a remainder.
        if (Vector256.IsHardwareAccelerated)
        {
            Vector256<float> v0 = block.V256_0;
            Vector256<float> v1 = block.V256_1;
            Vector256<float> v2 = block.V256_2;
            Vector256<float> v3 = block.V256_3;
            Vector256<float> v4 = block.V256_4;
            Vector256<float> v5 = block.V256_5;
            Vector256<float> v6 = block.V256_6;
            Vector256<float> v7 = block.V256_7;

            TOperator.Invoke(ref v0, ref v1, ref v2, ref v3, ref v4, ref v5, ref v6, ref v7);

            block.V256_0 = v0;
            block.V256_1 = v1;
            block.V256_2 = v2;
            block.V256_3 = v3;
            block.V256_4 = v4;
            block.V256_5 = v5;
            block.V256_6 = v6;
            block.V256_7 = v7;
        }
        else if (Vector128.IsHardwareAccelerated)
        {
            Vector128<float> l0 = block.V0L.AsVector128();
            Vector128<float> r0 = block.V0R.AsVector128();
            Vector128<float> l1 = block.V1L.AsVector128();
            Vector128<float> r1 = block.V1R.AsVector128();
            Vector128<float> l2 = block.V2L.AsVector128();
            Vector128<float> r2 = block.V2R.AsVector128();
            Vector128<float> l3 = block.V3L.AsVector128();
            Vector128<float> r3 = block.V3R.AsVector128();
            Vector128<float> l4 = block.V4L.AsVector128();
            Vector128<float> r4 = block.V4R.AsVector128();
            Vector128<float> l5 = block.V5L.AsVector128();
            Vector128<float> r5 = block.V5R.AsVector128();
            Vector128<float> l6 = block.V6L.AsVector128();
            Vector128<float> r6 = block.V6R.AsVector128();
            Vector128<float> l7 = block.V7L.AsVector128();
            Vector128<float> r7 = block.V7R.AsVector128();

            TOperator.Invoke(
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

            block.V0L = l0.AsVector4();
            block.V0R = r0.AsVector4();
            block.V1L = l1.AsVector4();
            block.V1R = r1.AsVector4();
            block.V2L = l2.AsVector4();
            block.V2R = r2.AsVector4();
            block.V3L = l3.AsVector4();
            block.V3R = r3.AsVector4();
            block.V4L = l4.AsVector4();
            block.V4R = r4.AsVector4();
            block.V5L = l5.AsVector4();
            block.V5R = r5.AsVector4();
            block.V6L = l6.AsVector4();
            block.V6R = r6.AsVector4();
            block.V7L = l7.AsVector4();
            block.V7R = r7.AsVector4();
        }
        else
        {
            TOperator.Invoke(ref block);
        }
    }

    /// <summary>
    /// Transposes a block: on return, row <c>i</c> holds column <c>i</c> of the input.
    /// </summary>
    internal readonly struct TransposeOperator : IBlockOperator
    {
        /// <inheritdoc/>
        public static void Invoke(ref Block8x8F block)
        {
            // The loop swaps each element above the diagonal with its mirror below the diagonal.
            ref float origin = ref Unsafe.As<Block8x8F, float>(ref block);
            for (nuint row = 0; row < 7; row++)
            {
                for (nuint column = row + 1; column < 8; column++)
                {
                    RuntimeUtility.Swap(ref Unsafe.Add(ref origin, (row * 8) + column), ref Unsafe.Add(ref origin, (column * 8) + row));
                }
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// The registers make four 4x4 tiles. Each <c>SimdUtils.Transpose4</c> call transposes one tile.
        /// The two off-diagonal tiles then change places.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke(
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
        {
            // Tile (rows 0-3, columns 0-3) stays in place. Tile (rows 4-7, columns 4-7) stays in place.
            SimdUtils.Transpose4(
                left0,
                left1,
                left2,
                left3,
                out Vector128<float> a0,
                out Vector128<float> a1,
                out Vector128<float> a2,
                out Vector128<float> a3);

            SimdUtils.Transpose4(
                right4,
                right5,
                right6,
                right7,
                out Vector128<float> d0,
                out Vector128<float> d1,
                out Vector128<float> d2,
                out Vector128<float> d3);

            // Tile (rows 0-3, columns 4-7) becomes tile (rows 4-7, columns 0-3), and the opposite.
            SimdUtils.Transpose4(
                right0,
                right1,
                right2,
                right3,
                out Vector128<float> b0,
                out Vector128<float> b1,
                out Vector128<float> b2,
                out Vector128<float> b3);

            SimdUtils.Transpose4(
                left4,
                left5,
                left6,
                left7,
                out Vector128<float> c0,
                out Vector128<float> c1,
                out Vector128<float> c2,
                out Vector128<float> c3);

            left0 = a0;
            left1 = a1;
            left2 = a2;
            left3 = a3;
            left4 = b0;
            left5 = b1;
            left6 = b2;
            left7 = b3;

            right0 = c0;
            right1 = c1;
            right2 = c2;
            right3 = c3;
            right4 = d0;
            right5 = d1;
            right6 = d2;
            right7 = d3;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Each 128-bit half of a row is one row of a 4x4 tile. Two <c>SimdUtils.Transpose4</c> calls transpose
        /// all four tiles: one call for rows 0 to 3, one call for rows 4 to 7. Then the halves recombine.
        /// The low halves make rows 0 to 3. The high halves make rows 4 to 7.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke(
            ref Vector256<float> v0,
            ref Vector256<float> v1,
            ref Vector256<float> v2,
            ref Vector256<float> v3,
            ref Vector256<float> v4,
            ref Vector256<float> v5,
            ref Vector256<float> v6,
            ref Vector256<float> v7)
        {
            // a[k] = [rows 0-3 of column k | rows 0-3 of column k + 4]
            // b[k] = [rows 4-7 of column k | rows 4-7 of column k + 4]
            SimdUtils.Transpose4(v0, v1, v2, v3, out Vector256<float> a0, out Vector256<float> a1, out Vector256<float> a2, out Vector256<float> a3);
            SimdUtils.Transpose4(v4, v5, v6, v7, out Vector256<float> b0, out Vector256<float> b1, out Vector256<float> b2, out Vector256<float> b3);

            v0 = Vector256_.CombineLower(a0, b0);
            v1 = Vector256_.CombineLower(a1, b1);
            v2 = Vector256_.CombineLower(a2, b2);
            v3 = Vector256_.CombineLower(a3, b3);
            v4 = Vector256_.CombineUpper(a0, b0);
            v5 = Vector256_.CombineUpper(a1, b1);
            v6 = Vector256_.CombineUpper(a2, b2);
            v7 = Vector256_.CombineUpper(a3, b3);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Each 128-bit group is one row of a 4x4 tile. Two <c>SimdUtils.Transpose4</c> calls transpose all eight tiles.
        /// Then one two-source permute for each register puts the 128-bit groups in order. The two blocks do not mix.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Invoke(
            ref Vector512<float> v0,
            ref Vector512<float> v1,
            ref Vector512<float> v2,
            ref Vector512<float> v3,
            ref Vector512<float> v4,
            ref Vector512<float> v5,
            ref Vector512<float> v6,
            ref Vector512<float> v7)
        {
            // The 128-bit groups of a[k] and b[k], with A the first block and B the second block:
            // a[k] = [A rows 0-3 of column k | A rows 0-3 of column k + 4 | B rows 0-3 of column k | B rows 0-3 of column k + 4]
            // b[k] = the same groups for rows 4-7.
            SimdUtils.Transpose4(v0, v1, v2, v3, out Vector512<float> a0, out Vector512<float> a1, out Vector512<float> a2, out Vector512<float> a3);
            SimdUtils.Transpose4(v4, v5, v6, v7, out Vector512<float> b0, out Vector512<float> b1, out Vector512<float> b2, out Vector512<float> b3);

            // Each 128-bit group is two 64-bit elements. Indices 0 to 7 select from a[k]. Indices 8 to 15 select from b[k].
            // Column k:     [a group 0 | b group 0 | a group 2 | b group 2]
            // Column k + 4: [a group 1 | b group 1 | a group 3 | b group 3]
            Vector512<long> lowColumns = Vector512.Create(0L, 1, 8, 9, 4, 5, 12, 13);
            Vector512<long> highColumns = Vector512.Create(2L, 3, 10, 11, 6, 7, 14, 15);

            v0 = Vector512_.PermuteVar8x64x2(a0.AsInt64(), lowColumns, b0.AsInt64()).AsSingle();
            v1 = Vector512_.PermuteVar8x64x2(a1.AsInt64(), lowColumns, b1.AsInt64()).AsSingle();
            v2 = Vector512_.PermuteVar8x64x2(a2.AsInt64(), lowColumns, b2.AsInt64()).AsSingle();
            v3 = Vector512_.PermuteVar8x64x2(a3.AsInt64(), lowColumns, b3.AsInt64()).AsSingle();
            v4 = Vector512_.PermuteVar8x64x2(a0.AsInt64(), highColumns, b0.AsInt64()).AsSingle();
            v5 = Vector512_.PermuteVar8x64x2(a1.AsInt64(), highColumns, b1.AsInt64()).AsSingle();
            v6 = Vector512_.PermuteVar8x64x2(a2.AsInt64(), highColumns, b2.AsInt64()).AsSingle();
            v7 = Vector512_.PermuteVar8x64x2(a3.AsInt64(), highColumns, b3.AsInt64()).AsSingle();
        }
    }
}
