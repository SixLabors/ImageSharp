// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Common.Helpers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Defines the quantization-matrix weighted transform error of the QM-PSNR distortion metric.
/// </content>
internal static partial class Av1TransformBlockEncoder
{
    /// <summary>
    /// Accumulates the weighted energy of the original coefficients and of the quantization error.
    /// </summary>
    /// <remarks>
    /// Every overload does the same lane-wise accumulation. Each weighted square gets its own rounding shift by the matrix precision.
    /// A weight is at most 34 and a coefficient of a 12-bit residual fits 21 bits. A weighted value or difference therefore fits a 32-bit lane.
    /// Only its square needs a 64-bit lane.
    /// </remarks>
    internal interface IAv1WeightedTransformErrorOperator
    {
        /// <summary>
        /// Accumulates one coefficient.
        /// </summary>
        /// <param name="value">The original coefficient.</param>
        /// <param name="dequantized">The reconstructed coefficient.</param>
        /// <param name="weight">The matrix weight of the coefficient.</param>
        /// <param name="energy">The running weighted energy of the original coefficients.</param>
        /// <param name="error">The running weighted energy of the quantization error.</param>
        public static abstract void Accumulate(int value, int dequantized, int weight, ref long energy, ref long error);

        /// <summary>
        /// Accumulates four coefficients.
        /// </summary>
        /// <param name="values">The original coefficients.</param>
        /// <param name="dequantized">The reconstructed coefficients.</param>
        /// <param name="weights">The matrix weights of the coefficients.</param>
        /// <param name="energies">The running weighted energies of the original coefficients.</param>
        /// <param name="errors">The running weighted energies of the quantization error.</param>
        public static abstract void Accumulate(
            Vector128<int> values,
            Vector128<int> dequantized,
            Vector128<int> weights,
            ref Vector128<long> energies,
            ref Vector128<long> errors);

        /// <inheritdoc cref="Accumulate(Vector128{int}, Vector128{int}, Vector128{int}, ref Vector128{long}, ref Vector128{long})"/>
        public static abstract void Accumulate(
            Vector256<int> values,
            Vector256<int> dequantized,
            Vector256<int> weights,
            ref Vector256<long> energies,
            ref Vector256<long> errors);

        /// <inheritdoc cref="Accumulate(Vector128{int}, Vector128{int}, Vector128{int}, ref Vector128{long}, ref Vector128{long})"/>
        public static abstract void Accumulate(
            Vector512<int> values,
            Vector512<int> dequantized,
            Vector512<int> weights,
            ref Vector512<long> energies,
            ref Vector512<long> errors);
    }

    /// <summary>
    /// Measures the weighted quantization error and energy in the transform distortion domain.
    /// </summary>
    /// <param name="coefficients">The original transform coefficients.</param>
    /// <param name="dequantized">The reconstructed transform coefficients.</param>
    /// <param name="transformSize">The transform dimensions controlling coefficient scaling.</param>
    /// <param name="bitDepth">The coded sample precision.</param>
    /// <param name="weights">
    /// The matrix weight of each coefficient, in the order of the distortion measure. See <see cref="Av1EncoderBlockWorkspace.GetDistortionWeights"/>.
    /// </param>
    /// <param name="sumOfSquares">The normalized weighted energy of the original coefficients.</param>
    /// <returns>The normalized weighted squared quantization error.</returns>
    public static long GetWeightedTransformError(
        ReadOnlySpan<int> coefficients,
        ReadOnlySpan<int> dequantized,
        Av1TransformSize transformSize,
        Av1BitDepth bitDepth,
        ReadOnlySpan<byte> weights,
        out long sumOfSquares)
    {
        WeightedTransformError<WeightedTransformErrorOperator>.Accumulate(
            coefficients, dequantized, weights, out long energy, out long error);

        // The rounding shift removes the extra precision of a high bit depth, so the squares are in 8-bit sample units.
        int precisionShift = 2 * (bitDepth.GetBitCount() - 8);
        long rounding = (1L << precisionShift) >> 1;
        error = (error + rounding) >> precisionShift;
        energy = (energy + rounding) >> precisionShift;

        // The scale shift puts the squares of every transform size in the units of a scale-1 transform. A scale-2 transform gives a
        // negative shift, which becomes a left shift.
        int scaleShift = (1 - transformSize.GetScale()) * 2;
        sumOfSquares = scaleShift >= 0 ? energy >> scaleShift : energy << -scaleShift;
        return scaleShift >= 0 ? error >> scaleShift : error << -scaleShift;
    }

    /// <summary>
    /// Squares the weighted coefficient and the weighted quantization error of each lane. Each square gets a rounding shift by the matrix precision.
    /// </summary>
    private readonly struct WeightedTransformErrorOperator : IAv1WeightedTransformErrorOperator
    {
        private const int Shift = 2 * Av1Constants.QuantizationMatrixElementBitCount;

        private const long Rounding = 1L << (Shift - 1);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Accumulate(int value, int dequantized, int weight, ref long energy, ref long error)
        {
            long weightedValue = (long)value * weight;
            long weightedDifference = ((long)value - dequantized) * weight;
            energy += ((weightedValue * weightedValue) + Rounding) >> Shift;
            error += ((weightedDifference * weightedDifference) + Rounding) >> Shift;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Accumulate(
            Vector128<int> values,
            Vector128<int> dequantized,
            Vector128<int> weights,
            ref Vector128<long> energies,
            ref Vector128<long> errors)
        {
            energies += RoundedSquares(values * weights);
            errors += RoundedSquares((values - dequantized) * weights);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Accumulate(
            Vector256<int> values,
            Vector256<int> dequantized,
            Vector256<int> weights,
            ref Vector256<long> energies,
            ref Vector256<long> errors)
        {
            energies += RoundedSquares(values * weights);
            errors += RoundedSquares((values - dequantized) * weights);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Accumulate(
            Vector512<int> values,
            Vector512<int> dequantized,
            Vector512<int> weights,
            ref Vector512<long> energies,
            ref Vector512<long> errors)
        {
            energies += RoundedSquares(values * weights);
            errors += RoundedSquares((values - dequantized) * weights);
        }

        /// <summary>
        /// Squares each lane into 64 bits and applies a rounding shift by the matrix precision. Then it adds the even and the odd lane of each pair.
        /// </summary>
        /// <param name="weighted">The weighted values.</param>
        /// <returns>The rounded squares, one sum per lane pair.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<long> RoundedSquares(Vector128<int> weighted)
        {
            // Each 64-bit pair holds an even lane in its low half and an odd lane in its high half. The logical 64-bit shift by 32 moves
            // each odd lane to the even position. The even-lane widening multiply then squares each lane into a 64-bit product.
            // Each product gets its own rounding shift before the add, as in the scalar overload. The vector width or order does not change the sum.
            Vector128<int> odd = (weighted.AsInt64() >>> 32).AsInt32();
            Vector128<long> rounding = Vector128.Create(Rounding);
            return ((Vector128_.MultiplyWideningEven(weighted, weighted) + rounding) >> Shift) +
                ((Vector128_.MultiplyWideningEven(odd, odd) + rounding) >> Shift);
        }

        /// <inheritdoc cref="RoundedSquares(Vector128{int})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<long> RoundedSquares(Vector256<int> weighted)
        {
            Vector256<int> odd = (weighted.AsInt64() >>> 32).AsInt32();
            Vector256<long> rounding = Vector256.Create(Rounding);
            return ((Vector256_.MultiplyWideningEven(weighted, weighted) + rounding) >> Shift) +
                ((Vector256_.MultiplyWideningEven(odd, odd) + rounding) >> Shift);
        }

        /// <inheritdoc cref="RoundedSquares(Vector128{int})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<long> RoundedSquares(Vector512<int> weighted)
        {
            Vector512<int> odd = (weighted.AsInt64() >>> 32).AsInt32();
            Vector512<long> rounding = Vector512.Create(Rounding);
            return ((Vector512_.MultiplyWideningEven(weighted, weighted) + rounding) >> Shift) +
                ((Vector512_.MultiplyWideningEven(odd, odd) + rounding) >> Shift);
        }
    }

    /// <summary>
    /// Reduces one transform block with the arithmetic of a closed weighted accumulation operator.
    /// </summary>
    /// <typeparam name="TOperator">The lane-wise accumulation.</typeparam>
    private static class WeightedTransformError<TOperator>
        where TOperator : struct, IAv1WeightedTransformErrorOperator
    {
        /// <summary>
        /// Accumulates the weighted energy and quantization error of one transform block.
        /// </summary>
        /// <param name="coefficients">The original transform coefficients.</param>
        /// <param name="dequantized">The reconstructed transform coefficients.</param>
        /// <param name="weights">The matrix weight of each coefficient.</param>
        /// <param name="energy">Receives the weighted energy of the original coefficients.</param>
        /// <param name="error">Receives the weighted energy of the quantization error.</param>
        public static void Accumulate(
            ReadOnlySpan<int> coefficients,
            ReadOnlySpan<int> dequantized,
            ReadOnlySpan<byte> weights,
            out long energy,
            out long error)
        {
            ref int coefficientBase = ref MemoryMarshal.GetReference(coefficients);
            ref int dequantizedBase = ref MemoryMarshal.GetReference(dequantized);
            ref byte weightBase = ref MemoryMarshal.GetReference(weights);
            energy = 0;
            error = 0;
            int i = 0;

            // Each stage widens the byte weights to 32-bit lanes, so lane n of the weights lines up with lane n of the coefficients.
            if (Vector512.IsHardwareAccelerated)
            {
                Vector512<long> energies = Vector512<long>.Zero;
                Vector512<long> errors = Vector512<long>.Zero;
                for (; i <= coefficients.Length - Vector512<int>.Count; i += Vector512<int>.Count)
                {
                    TOperator.Accumulate(
                        Vector512.LoadUnsafe(ref coefficientBase, (nuint)i),
                        Vector512.LoadUnsafe(ref dequantizedBase, (nuint)i),
                        Av1ForwardQuantizer.LoadWeights(ref Unsafe.Add(ref weightBase, i), Vector512<int>.Zero),
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
                        Av1ForwardQuantizer.LoadWeights(ref Unsafe.Add(ref weightBase, i), Vector256<int>.Zero),
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
                        Av1ForwardQuantizer.LoadWeights(ref Unsafe.Add(ref weightBase, i), Vector128<int>.Zero),
                        ref energies,
                        ref errors);
                }

                energy += Vector128.Sum(energies);
                error += Vector128.Sum(errors);
            }

            // The scalar overload accumulates the coefficients that no vector stage covered.
            for (; i < coefficients.Length; i++)
            {
                TOperator.Accumulate(coefficients[i], dequantized[i], weights[i], ref energy, ref error);
            }
        }
    }
}
