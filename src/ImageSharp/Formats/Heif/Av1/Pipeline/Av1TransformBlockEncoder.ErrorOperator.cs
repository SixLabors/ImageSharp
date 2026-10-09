// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Defines the arithmetic contract consumed by the shared transform-error traversal.
/// </content>
internal static partial class Av1TransformBlockEncoder
{
    /// <summary>
    /// Accumulates the energy of the original coefficients and the energy of the quantization error.
    /// </summary>
    /// <remarks>
    /// Every overload does the same lane-wise accumulation. A coefficient and a difference both fit a 32-bit lane.
    /// Their squares do not fit for a 12-bit transform. Each overload therefore widens to 64-bit lanes before it squares and accumulates.
    /// </remarks>
    internal interface IAv1TransformErrorOperator
    {
        /// <summary>
        /// Accumulates one coefficient.
        /// </summary>
        /// <param name="value">The original coefficient.</param>
        /// <param name="dequantized">The reconstructed coefficient.</param>
        /// <param name="energy">The running energy of the original coefficients.</param>
        /// <param name="error">The running energy of the quantization error.</param>
        public static abstract void Accumulate(int value, int dequantized, ref long energy, ref long error);

        /// <summary>
        /// Accumulates four coefficients.
        /// </summary>
        /// <param name="values">The original coefficients.</param>
        /// <param name="dequantized">The reconstructed coefficients.</param>
        /// <param name="energies">The running energies of the original coefficients.</param>
        /// <param name="errors">The running energies of the quantization error.</param>
        public static abstract void Accumulate(
            Vector128<int> values,
            Vector128<int> dequantized,
            ref Vector128<long> energies,
            ref Vector128<long> errors);

        /// <summary>
        /// Accumulates eight coefficients.
        /// </summary>
        /// <param name="values">The original coefficients.</param>
        /// <param name="dequantized">The reconstructed coefficients.</param>
        /// <param name="energies">The running energies of the original coefficients.</param>
        /// <param name="errors">The running energies of the quantization error.</param>
        public static abstract void Accumulate(
            Vector256<int> values,
            Vector256<int> dequantized,
            ref Vector256<long> energies,
            ref Vector256<long> errors);

        /// <summary>
        /// Accumulates sixteen coefficients.
        /// </summary>
        /// <param name="values">The original coefficients.</param>
        /// <param name="dequantized">The reconstructed coefficients.</param>
        /// <param name="energies">The running energies of the original coefficients.</param>
        /// <param name="errors">The running energies of the quantization error.</param>
        public static abstract void Accumulate(
            Vector512<int> values,
            Vector512<int> dequantized,
            ref Vector512<long> energies,
            ref Vector512<long> errors);
    }

    /// <summary>
    /// Squares the coefficient and the quantization error of each lane.
    /// </summary>
    /// <remarks>
    /// The scalar overload is the exact form. The vector overloads give the same totals because integer addition is associative.
    /// </remarks>
    private readonly struct TransformErrorOperator : IAv1TransformErrorOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Accumulate(int value, int dequantized, ref long energy, ref long error)
        {
            long wide = value;
            long difference = wide - dequantized;
            energy += wide * wide;
            error += difference * difference;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Accumulate(
            Vector128<int> values,
            Vector128<int> dequantized,
            ref Vector128<long> energies,
            ref Vector128<long> errors)
        {
            // Each lane is one independent coefficient in raster order. The code subtracts before it widens, because the difference fits a
            // 32-bit lane. Only the squares need the 64-bit lanes.
            Vector128<int> differences = values - dequantized;
            (Vector128<long> lower, Vector128<long> upper) = Vector128.Widen(values);
            (Vector128<long> lowerDifference, Vector128<long> upperDifference) = Vector128.Widen(differences);
            energies += (lower * lower) + (upper * upper);
            errors += (lowerDifference * lowerDifference) + (upperDifference * upperDifference);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Accumulate(
            Vector256<int> values,
            Vector256<int> dequantized,
            ref Vector256<long> energies,
            ref Vector256<long> errors)
        {
            // Eight independent coefficients, with the lane layout and the arithmetic of the 128-bit overload.
            Vector256<int> differences = values - dequantized;
            (Vector256<long> lower, Vector256<long> upper) = Vector256.Widen(values);
            (Vector256<long> lowerDifference, Vector256<long> upperDifference) = Vector256.Widen(differences);
            energies += (lower * lower) + (upper * upper);
            errors += (lowerDifference * lowerDifference) + (upperDifference * upperDifference);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Accumulate(
            Vector512<int> values,
            Vector512<int> dequantized,
            ref Vector512<long> energies,
            ref Vector512<long> errors)
        {
            // Sixteen independent coefficients, with the lane layout and the arithmetic of the 128-bit overload. One subtraction in 32-bit
            // lanes replaces two subtractions in 64-bit lanes after the widening.
            Vector512<int> differences = values - dequantized;
            (Vector512<long> lower, Vector512<long> upper) = Vector512.Widen(values);
            (Vector512<long> lowerDifference, Vector512<long> upperDifference) = Vector512.Widen(differences);
            energies += (lower * lower) + (upper * upper);
            errors += (lowerDifference * lowerDifference) + (upperDifference * upperDifference);
        }
    }

    /// <summary>
    /// Reduces one transform block with the arithmetic of a closed accumulation operator.
    /// </summary>
    /// <typeparam name="TOperator">The lane-wise accumulation.</typeparam>
    /// <remarks>
    /// One lane is one coefficient in raster order. The traversal walks the block at descending register widths and ends with a scalar tail.
    /// Each width keeps its own vector of running totals and folds it once, at the end of its stage.
    /// Integer addition is associative, so the lane order does not change the totals.
    /// </remarks>
    private static class TransformError<TOperator>
        where TOperator : struct, IAv1TransformErrorOperator
    {
        /// <summary>
        /// Accumulates the energy and the quantization error of one transform block.
        /// </summary>
        /// <param name="coefficients">The original transform coefficients.</param>
        /// <param name="dequantized">The reconstructed transform coefficients.</param>
        /// <param name="energy">Receives the energy of the original coefficients.</param>
        /// <param name="error">Receives the energy of the quantization error.</param>
        public static void Accumulate(
            ReadOnlySpan<int> coefficients,
            ReadOnlySpan<int> dequantized,
            out long energy,
            out long error)
        {
            ref int coefficientBase = ref MemoryMarshal.GetReference(coefficients);
            ref int dequantizedBase = ref MemoryMarshal.GetReference(dequantized);
            energy = 0;
            error = 0;
            int i = 0;

            if (Vector512.IsHardwareAccelerated)
            {
                Vector512<long> energies = Vector512<long>.Zero;
                Vector512<long> errors = Vector512<long>.Zero;
                for (; i <= coefficients.Length - Vector512<int>.Count; i += Vector512<int>.Count)
                {
                    TOperator.Accumulate(
                        Vector512.LoadUnsafe(ref coefficientBase, (nuint)i),
                        Vector512.LoadUnsafe(ref dequantizedBase, (nuint)i),
                        ref energies,
                        ref errors);
                }

                energy += Vector512.Sum(energies);
                error += Vector512.Sum(errors);
            }

            if (Vector256.IsHardwareAccelerated)
            {
                Vector256<long> energies = Vector256<long>.Zero;
                Vector256<long> errors = Vector256<long>.Zero;
                for (; i <= coefficients.Length - Vector256<int>.Count; i += Vector256<int>.Count)
                {
                    TOperator.Accumulate(
                        Vector256.LoadUnsafe(ref coefficientBase, (nuint)i),
                        Vector256.LoadUnsafe(ref dequantizedBase, (nuint)i),
                        ref energies,
                        ref errors);
                }

                energy += Vector256.Sum(energies);
                error += Vector256.Sum(errors);
            }

            if (Vector128.IsHardwareAccelerated)
            {
                Vector128<long> energies = Vector128<long>.Zero;
                Vector128<long> errors = Vector128<long>.Zero;
                for (; i <= coefficients.Length - Vector128<int>.Count; i += Vector128<int>.Count)
                {
                    TOperator.Accumulate(
                        Vector128.LoadUnsafe(ref coefficientBase, (nuint)i),
                        Vector128.LoadUnsafe(ref dequantizedBase, (nuint)i),
                        ref energies,
                        ref errors);
                }

                energy += Vector128.Sum(energies);
                error += Vector128.Sum(errors);
            }

            // The scalar overload accumulates the coefficients that no vector stage covered.
            for (; i < coefficients.Length; i++)
            {
                TOperator.Accumulate(coefficients[i], dequantized[i], ref energy, ref error);
            }
        }
    }
}
