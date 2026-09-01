// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1CompoundInterPredictor;
using static SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter.Av1TranslationalInterPredictor;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

/// <content>
/// Defines alpha-masked compound prediction arithmetic.
/// </content>
internal static partial class Av1CompoundMaskBlendPredictor
{
    /// <summary>
    /// Defines alpha-masked compound blending for scalar and SIMD lane groups.
    /// </summary>
    private interface IAv1CompoundMaskBlendOperator
    {
        /// <summary>
        /// Blends two 8-bit samples through an AV1 alpha value.
        /// </summary>
        /// <param name="first">The first sample.</param>
        /// <param name="second">The second sample.</param>
        /// <param name="alpha">The first-sample weight in the AV1 mask range.</param>
        /// <returns>The blended sample.</returns>
        public static abstract byte Blend(byte first, byte second, byte alpha);

        /// <summary>
        /// Blends two high-bit-depth samples through an AV1 alpha value.
        /// </summary>
        /// <param name="first">The first sample.</param>
        /// <param name="second">The second sample.</param>
        /// <param name="alpha">The first-sample weight in the AV1 mask range.</param>
        /// <returns>The blended sample.</returns>
        public static abstract ushort Blend(ushort first, ushort second, byte alpha);

        /// <summary>
        /// Blends 128-bit vectors of 8-bit samples through AV1 alpha values.
        /// </summary>
        /// <param name="first">The first samples.</param>
        /// <param name="second">The second samples.</param>
        /// <param name="alpha">The first-sample weights in the AV1 mask range.</param>
        /// <returns>The blended samples.</returns>
        public static abstract Vector128<byte> Blend(Vector128<byte> first, Vector128<byte> second, Vector128<byte> alpha);

        /// <summary>
        /// Blends 256-bit vectors of 8-bit samples through AV1 alpha values.
        /// </summary>
        /// <param name="first">The first samples.</param>
        /// <param name="second">The second samples.</param>
        /// <param name="alpha">The first-sample weights in the AV1 mask range.</param>
        /// <returns>The blended samples.</returns>
        public static abstract Vector256<byte> Blend(Vector256<byte> first, Vector256<byte> second, Vector256<byte> alpha);

        /// <summary>
        /// Blends 512-bit vectors of 8-bit samples through AV1 alpha values.
        /// </summary>
        /// <param name="first">The first samples.</param>
        /// <param name="second">The second samples.</param>
        /// <param name="alpha">The first-sample weights in the AV1 mask range.</param>
        /// <returns>The blended samples.</returns>
        public static abstract Vector512<byte> Blend(Vector512<byte> first, Vector512<byte> second, Vector512<byte> alpha);

        /// <summary>
        /// Blends 128-bit vectors of high-bit-depth samples through AV1 alpha values.
        /// </summary>
        /// <param name="first">The first samples.</param>
        /// <param name="second">The second samples.</param>
        /// <param name="alpha">The first-sample weights in the AV1 mask range.</param>
        /// <returns>The blended samples.</returns>
        public static abstract Vector128<ushort> Blend(Vector128<ushort> first, Vector128<ushort> second, Vector128<ushort> alpha);

        /// <summary>
        /// Blends 256-bit vectors of high-bit-depth samples through AV1 alpha values.
        /// </summary>
        /// <param name="first">The first samples.</param>
        /// <param name="second">The second samples.</param>
        /// <param name="alpha">The first-sample weights in the AV1 mask range.</param>
        /// <returns>The blended samples.</returns>
        public static abstract Vector256<ushort> Blend(Vector256<ushort> first, Vector256<ushort> second, Vector256<ushort> alpha);

        /// <summary>
        /// Blends 512-bit vectors of high-bit-depth samples through AV1 alpha values.
        /// </summary>
        /// <param name="first">The first samples.</param>
        /// <param name="second">The second samples.</param>
        /// <param name="alpha">The first-sample weights in the AV1 mask range.</param>
        /// <returns>The blended samples.</returns>
        public static abstract Vector512<ushort> Blend(Vector512<ushort> first, Vector512<ushort> second, Vector512<ushort> alpha);
    }

    /// <summary>
    /// Implements AV1 alpha-mask blending for scalar and SIMD lane groups.
    /// </summary>
    private readonly struct CompoundMaskBlendOperator : IAv1CompoundMaskBlendOperator
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Blend(byte first, byte second, byte alpha)
            => (byte)(((alpha * first) + ((MaximumMaskAlpha - alpha) * second) + 32) >> MaskWeightBits);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort Blend(ushort first, ushort second, byte alpha)
            => (ushort)(((alpha * first) + ((MaximumMaskAlpha - alpha) * second) + 32) >> MaskWeightBits);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Blend(Vector128<byte> first, Vector128<byte> second, Vector128<byte> alpha)
        {
            Av1NonDirectionalIntraPredictorBase.Widen(first, out Vector128<int> first0, out Vector128<int> first1, out Vector128<int> first2, out Vector128<int> first3);
            Av1NonDirectionalIntraPredictorBase.Widen(second, out Vector128<int> second0, out Vector128<int> second1, out Vector128<int> second2, out Vector128<int> second3);
            Av1NonDirectionalIntraPredictorBase.Widen(alpha, out Vector128<int> alpha0, out Vector128<int> alpha1, out Vector128<int> alpha2, out Vector128<int> alpha3);
            return Av1NonDirectionalIntraPredictorBase.Narrow(
                Blend(first0, second0, alpha0),
                Blend(first1, second1, alpha1),
                Blend(first2, second2, alpha2),
                Blend(first3, second3, alpha3));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> Blend(Vector256<byte> first, Vector256<byte> second, Vector256<byte> alpha)
        {
            Av1NonDirectionalIntraPredictorBase.Widen(first, out Vector256<int> first0, out Vector256<int> first1, out Vector256<int> first2, out Vector256<int> first3);
            Av1NonDirectionalIntraPredictorBase.Widen(second, out Vector256<int> second0, out Vector256<int> second1, out Vector256<int> second2, out Vector256<int> second3);
            Av1NonDirectionalIntraPredictorBase.Widen(alpha, out Vector256<int> alpha0, out Vector256<int> alpha1, out Vector256<int> alpha2, out Vector256<int> alpha3);
            return Av1NonDirectionalIntraPredictorBase.Narrow(
                Blend(first0, second0, alpha0),
                Blend(first1, second1, alpha1),
                Blend(first2, second2, alpha2),
                Blend(first3, second3, alpha3));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<byte> Blend(Vector512<byte> first, Vector512<byte> second, Vector512<byte> alpha)
        {
            Av1NonDirectionalIntraPredictorBase.Widen(first, out Vector512<int> first0, out Vector512<int> first1, out Vector512<int> first2, out Vector512<int> first3);
            Av1NonDirectionalIntraPredictorBase.Widen(second, out Vector512<int> second0, out Vector512<int> second1, out Vector512<int> second2, out Vector512<int> second3);
            Av1NonDirectionalIntraPredictorBase.Widen(alpha, out Vector512<int> alpha0, out Vector512<int> alpha1, out Vector512<int> alpha2, out Vector512<int> alpha3);
            return Av1NonDirectionalIntraPredictorBase.Narrow(
                Blend(first0, second0, alpha0),
                Blend(first1, second1, alpha1),
                Blend(first2, second2, alpha2),
                Blend(first3, second3, alpha3));
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<ushort> Blend(Vector128<ushort> first, Vector128<ushort> second, Vector128<ushort> alpha)
        {
            Av1NonDirectionalIntraPredictorBase.Widen(first.AsInt16(), out Vector128<int> first0, out Vector128<int> first1);
            Av1NonDirectionalIntraPredictorBase.Widen(second.AsInt16(), out Vector128<int> second0, out Vector128<int> second1);
            Av1NonDirectionalIntraPredictorBase.Widen(alpha.AsInt16(), out Vector128<int> alpha0, out Vector128<int> alpha1);
            return Av1NonDirectionalIntraPredictorBase.Narrow(Blend(first0, second0, alpha0), Blend(first1, second1, alpha1)).AsUInt16();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<ushort> Blend(Vector256<ushort> first, Vector256<ushort> second, Vector256<ushort> alpha)
        {
            Av1NonDirectionalIntraPredictorBase.Widen(first.AsInt16(), out Vector256<int> first0, out Vector256<int> first1);
            Av1NonDirectionalIntraPredictorBase.Widen(second.AsInt16(), out Vector256<int> second0, out Vector256<int> second1);
            Av1NonDirectionalIntraPredictorBase.Widen(alpha.AsInt16(), out Vector256<int> alpha0, out Vector256<int> alpha1);
            return Av1NonDirectionalIntraPredictorBase.Narrow(Blend(first0, second0, alpha0), Blend(first1, second1, alpha1)).AsUInt16();
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<ushort> Blend(Vector512<ushort> first, Vector512<ushort> second, Vector512<ushort> alpha)
        {
            Av1NonDirectionalIntraPredictorBase.Widen(first.AsInt16(), out Vector512<int> first0, out Vector512<int> first1);
            Av1NonDirectionalIntraPredictorBase.Widen(second.AsInt16(), out Vector512<int> second0, out Vector512<int> second1);
            Av1NonDirectionalIntraPredictorBase.Widen(alpha.AsInt16(), out Vector512<int> alpha0, out Vector512<int> alpha1);
            return Av1NonDirectionalIntraPredictorBase.Narrow(Blend(first0, second0, alpha0), Blend(first1, second1, alpha1)).AsUInt16();
        }

        /// <summary>
        /// Applies alpha-mask blending to 128-bit vectors of widened samples.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<int> Blend(Vector128<int> first, Vector128<int> second, Vector128<int> alpha)
            => ((alpha * first) + ((Vector128.Create(MaximumMaskAlpha) - alpha) * second) + Vector128.Create(32)) >> MaskWeightBits;

        /// <summary>
        /// Applies alpha-mask blending to 256-bit vectors of widened samples.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<int> Blend(Vector256<int> first, Vector256<int> second, Vector256<int> alpha)
            => ((alpha * first) + ((Vector256.Create(MaximumMaskAlpha) - alpha) * second) + Vector256.Create(32)) >> MaskWeightBits;

        /// <summary>
        /// Applies alpha-mask blending to 512-bit vectors of widened samples.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<int> Blend(Vector512<int> first, Vector512<int> second, Vector512<int> alpha)
            => ((alpha * first) + ((Vector512.Create(MaximumMaskAlpha) - alpha) * second) + Vector512.Create(32)) >> MaskWeightBits;
    }
}
