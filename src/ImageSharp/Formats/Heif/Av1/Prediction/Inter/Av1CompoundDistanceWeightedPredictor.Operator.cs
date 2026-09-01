// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1CompoundInterPredictor;
using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1TranslationalInterPredictor;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Defines distance-weighted compound prediction arithmetic.
/// </content>
internal static partial class Av1CompoundDistanceWeightedPredictor
{
    /// <summary>
    /// Defines distance-weighted compound blending for scalar and SIMD lane groups.
    /// </summary>
    private interface IAv1CompoundDistanceWeightedOperator
    {
        /// <summary>
        /// Blends two 8-bit samples with display-distance weights.
        /// </summary>
        /// <param name="first">The first sample.</param>
        /// <param name="second">The second sample.</param>
        /// <param name="firstWeight">The first predictor weight.</param>
        /// <param name="secondWeight">The second predictor weight.</param>
        /// <returns>The weighted sample.</returns>
        public static abstract byte Blend(byte first, byte second, int firstWeight, int secondWeight);

        /// <summary>
        /// Blends two high-bit-depth samples with display-distance weights.
        /// </summary>
        /// <param name="first">The first sample.</param>
        /// <param name="second">The second sample.</param>
        /// <param name="firstWeight">The first predictor weight.</param>
        /// <param name="secondWeight">The second predictor weight.</param>
        /// <returns>The weighted sample.</returns>
        public static abstract ushort Blend(ushort first, ushort second, int firstWeight, int secondWeight);

        /// <summary>
        /// Blends 128-bit vectors of 8-bit samples with display-distance weights.
        /// </summary>
        /// <param name="first">The first samples.</param>
        /// <param name="second">The second samples.</param>
        /// <param name="firstWeight">The first predictor weight.</param>
        /// <param name="secondWeight">The second predictor weight.</param>
        /// <returns>The weighted samples.</returns>
        public static abstract Vector128<byte> Blend(Vector128<byte> first, Vector128<byte> second, int firstWeight, int secondWeight);

        /// <summary>
        /// Blends 256-bit vectors of 8-bit samples with display-distance weights.
        /// </summary>
        /// <param name="first">The first samples.</param>
        /// <param name="second">The second samples.</param>
        /// <param name="firstWeight">The first predictor weight.</param>
        /// <param name="secondWeight">The second predictor weight.</param>
        /// <returns>The weighted samples.</returns>
        public static abstract Vector256<byte> Blend(Vector256<byte> first, Vector256<byte> second, int firstWeight, int secondWeight);

        /// <summary>
        /// Blends 512-bit vectors of 8-bit samples with display-distance weights.
        /// </summary>
        /// <param name="first">The first samples.</param>
        /// <param name="second">The second samples.</param>
        /// <param name="firstWeight">The first predictor weight.</param>
        /// <param name="secondWeight">The second predictor weight.</param>
        /// <returns>The weighted samples.</returns>
        public static abstract Vector512<byte> Blend(Vector512<byte> first, Vector512<byte> second, int firstWeight, int secondWeight);

        /// <summary>
        /// Blends 128-bit vectors of high-bit-depth samples with display-distance weights.
        /// </summary>
        /// <param name="first">The first samples.</param>
        /// <param name="second">The second samples.</param>
        /// <param name="firstWeight">The first predictor weight.</param>
        /// <param name="secondWeight">The second predictor weight.</param>
        /// <returns>The weighted samples.</returns>
        public static abstract Vector128<ushort> Blend(Vector128<ushort> first, Vector128<ushort> second, int firstWeight, int secondWeight);

        /// <summary>
        /// Blends 256-bit vectors of high-bit-depth samples with display-distance weights.
        /// </summary>
        /// <param name="first">The first samples.</param>
        /// <param name="second">The second samples.</param>
        /// <param name="firstWeight">The first predictor weight.</param>
        /// <param name="secondWeight">The second predictor weight.</param>
        /// <returns>The weighted samples.</returns>
        public static abstract Vector256<ushort> Blend(Vector256<ushort> first, Vector256<ushort> second, int firstWeight, int secondWeight);

        /// <summary>
        /// Blends 512-bit vectors of high-bit-depth samples with display-distance weights.
        /// </summary>
        /// <param name="first">The first samples.</param>
        /// <param name="second">The second samples.</param>
        /// <param name="firstWeight">The first predictor weight.</param>
        /// <param name="secondWeight">The second predictor weight.</param>
        /// <returns>The weighted samples.</returns>
        public static abstract Vector512<ushort> Blend(Vector512<ushort> first, Vector512<ushort> second, int firstWeight, int secondWeight);
    }

    /// <summary>
    /// Implements display-distance-weighted blending for scalar and SIMD lane groups.
    /// </summary>
    private readonly struct CompoundDistanceWeightedOperator : IAv1CompoundDistanceWeightedOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Blend(byte first, byte second, int firstWeight, int secondWeight)
            => (byte)(((first * firstWeight) + (second * secondWeight) + 8) >> DistanceWeightBits);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort Blend(ushort first, ushort second, int firstWeight, int secondWeight)
            => (ushort)(((first * firstWeight) + (second * secondWeight) + 8) >> DistanceWeightBits);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Blend(Vector128<byte> first, Vector128<byte> second, int firstWeight, int secondWeight)
        {
            Av1NonDirectionalIntraPredictorBase.Widen(first, out Vector128<int> first0, out Vector128<int> first1, out Vector128<int> first2, out Vector128<int> first3);
            Av1NonDirectionalIntraPredictorBase.Widen(second, out Vector128<int> second0, out Vector128<int> second1, out Vector128<int> second2, out Vector128<int> second3);
            return Av1NonDirectionalIntraPredictorBase.Narrow(
                Blend(first0, second0, firstWeight, secondWeight),
                Blend(first1, second1, firstWeight, secondWeight),
                Blend(first2, second2, firstWeight, secondWeight),
                Blend(first3, second3, firstWeight, secondWeight));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> Blend(Vector256<byte> first, Vector256<byte> second, int firstWeight, int secondWeight)
        {
            Av1NonDirectionalIntraPredictorBase.Widen(first, out Vector256<int> first0, out Vector256<int> first1, out Vector256<int> first2, out Vector256<int> first3);
            Av1NonDirectionalIntraPredictorBase.Widen(second, out Vector256<int> second0, out Vector256<int> second1, out Vector256<int> second2, out Vector256<int> second3);
            return Av1NonDirectionalIntraPredictorBase.Narrow(
                Blend(first0, second0, firstWeight, secondWeight),
                Blend(first1, second1, firstWeight, secondWeight),
                Blend(first2, second2, firstWeight, secondWeight),
                Blend(first3, second3, firstWeight, secondWeight));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<byte> Blend(Vector512<byte> first, Vector512<byte> second, int firstWeight, int secondWeight)
        {
            Av1NonDirectionalIntraPredictorBase.Widen(first, out Vector512<int> first0, out Vector512<int> first1, out Vector512<int> first2, out Vector512<int> first3);
            Av1NonDirectionalIntraPredictorBase.Widen(second, out Vector512<int> second0, out Vector512<int> second1, out Vector512<int> second2, out Vector512<int> second3);
            return Av1NonDirectionalIntraPredictorBase.Narrow(
                Blend(first0, second0, firstWeight, secondWeight),
                Blend(first1, second1, firstWeight, secondWeight),
                Blend(first2, second2, firstWeight, secondWeight),
                Blend(first3, second3, firstWeight, secondWeight));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ushort> Blend(Vector128<ushort> first, Vector128<ushort> second, int firstWeight, int secondWeight)
        {
            Av1NonDirectionalIntraPredictorBase.Widen(first.AsInt16(), out Vector128<int> first0, out Vector128<int> first1);
            Av1NonDirectionalIntraPredictorBase.Widen(second.AsInt16(), out Vector128<int> second0, out Vector128<int> second1);
            return Av1NonDirectionalIntraPredictorBase.Narrow(
                Blend(first0, second0, firstWeight, secondWeight),
                Blend(first1, second1, firstWeight, secondWeight)).AsUInt16();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ushort> Blend(Vector256<ushort> first, Vector256<ushort> second, int firstWeight, int secondWeight)
        {
            Av1NonDirectionalIntraPredictorBase.Widen(first.AsInt16(), out Vector256<int> first0, out Vector256<int> first1);
            Av1NonDirectionalIntraPredictorBase.Widen(second.AsInt16(), out Vector256<int> second0, out Vector256<int> second1);
            return Av1NonDirectionalIntraPredictorBase.Narrow(
                Blend(first0, second0, firstWeight, secondWeight),
                Blend(first1, second1, firstWeight, secondWeight)).AsUInt16();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<ushort> Blend(Vector512<ushort> first, Vector512<ushort> second, int firstWeight, int secondWeight)
        {
            Av1NonDirectionalIntraPredictorBase.Widen(first.AsInt16(), out Vector512<int> first0, out Vector512<int> first1);
            Av1NonDirectionalIntraPredictorBase.Widen(second.AsInt16(), out Vector512<int> second0, out Vector512<int> second1);
            return Av1NonDirectionalIntraPredictorBase.Narrow(
                Blend(first0, second0, firstWeight, secondWeight),
                Blend(first1, second1, firstWeight, secondWeight)).AsUInt16();
        }

        /// <summary>
        /// Applies display-distance weighting to 128-bit vectors of widened samples.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<int> Blend(Vector128<int> first, Vector128<int> second, int firstWeight, int secondWeight)
            => ((first * Vector128.Create(firstWeight)) + (second * Vector128.Create(secondWeight)) + Vector128.Create(8)) >> DistanceWeightBits;

        /// <summary>
        /// Applies display-distance weighting to 256-bit vectors of widened samples.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<int> Blend(Vector256<int> first, Vector256<int> second, int firstWeight, int secondWeight)
            => ((first * Vector256.Create(firstWeight)) + (second * Vector256.Create(secondWeight)) + Vector256.Create(8)) >> DistanceWeightBits;

        /// <summary>
        /// Applies display-distance weighting to 512-bit vectors of widened samples.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<int> Blend(Vector512<int> first, Vector512<int> second, int firstWeight, int secondWeight)
            => ((first * Vector512.Create(firstWeight)) + (second * Vector512.Create(secondWeight)) + Vector512.Create(8)) >> DistanceWeightBits;
    }
}
