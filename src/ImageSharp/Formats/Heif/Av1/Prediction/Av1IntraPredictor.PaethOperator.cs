// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <content>
/// Provides Paeth intra prediction for scalar and SIMD sample representations.
/// </content>
internal abstract partial class Av1IntraPredictorBase
{
    /// <summary>
    /// Implements AV1 Paeth intra prediction for scalar and SIMD lanes.
    /// </summary>
    internal readonly struct PaethOperator : IAv1IntraPredictionOperator
    {
        /// <inheritdoc/>
        public static Av1PredictionMode Mode => Av1PredictionMode.Paeth;

        /// <inheritdoc/>
        public static Av1IntraPredictionInputs Inputs
            => Av1IntraPredictionInputs.Top | Av1IntraPredictionInputs.Left | Av1IntraPredictionInputs.TopLeft;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Predict(byte top, byte left, byte topLeft, byte topRight, byte bottomLeft, int columnWeight, int rowWeight)
        {
            int basis = top + left - topLeft;
            int distanceLeft = Math.Abs(basis - left);
            int distanceTop = Math.Abs(basis - top);
            int distanceTopLeft = Math.Abs(basis - topLeft);

            // AV1 resolves equal distances in left, top, top-left order.
            return distanceLeft <= distanceTop && distanceLeft <= distanceTopLeft ? left : distanceTop <= distanceTopLeft ? top : topLeft;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Predict(Vector128<byte> top, Vector128<byte> left, Vector128<byte> topLeft, Vector128<byte> topRight, Vector128<byte> bottomLeft, ref int columnWeights, int rowWeight)
            => PredictPaeth(top, left, topLeft);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> Predict(Vector256<byte> top, Vector256<byte> left, Vector256<byte> topLeft, Vector256<byte> topRight, Vector256<byte> bottomLeft, ref int columnWeights, int rowWeight)
            => PredictPaeth(top, left, topLeft);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<byte> Predict(Vector512<byte> top, Vector512<byte> left, Vector512<byte> topLeft, Vector512<byte> topRight, Vector512<byte> bottomLeft, ref int columnWeights, int rowWeight)
            => PredictPaeth(top, left, topLeft);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short Predict(short top, short left, short topLeft, short topRight, short bottomLeft, int columnWeight, int rowWeight)
        {
            int basis = top + left - topLeft;
            int distanceLeft = Math.Abs(basis - left);
            int distanceTop = Math.Abs(basis - top);
            int distanceTopLeft = Math.Abs(basis - topLeft);

            // High-bit-depth prediction has the same tie order as the 8-bit process.
            return distanceLeft <= distanceTop && distanceLeft <= distanceTopLeft ? left : distanceTop <= distanceTopLeft ? top : topLeft;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<short> Predict(Vector128<short> top, Vector128<short> left, Vector128<short> topLeft, Vector128<short> topRight, Vector128<short> bottomLeft, ref int columnWeights, int rowWeight)
            => PredictPaeth(top, left, topLeft);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<short> Predict(Vector256<short> top, Vector256<short> left, Vector256<short> topLeft, Vector256<short> topRight, Vector256<short> bottomLeft, ref int columnWeights, int rowWeight)
            => PredictPaeth(top, left, topLeft);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<short> Predict(Vector512<short> top, Vector512<short> left, Vector512<short> topLeft, Vector512<short> topRight, Vector512<short> bottomLeft, ref int columnWeights, int rowWeight)
            => PredictPaeth(top, left, topLeft);

        /// <summary>
        /// Selects the nearest Paeth reference for sixteen 8-bit lanes.
        /// </summary>
        /// <param name="top">The top candidates.</param>
        /// <param name="left">The left candidates.</param>
        /// <param name="topLeft">The top-left candidates.</param>
        /// <returns>The selected candidates.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<byte> PredictPaeth(Vector128<byte> top, Vector128<byte> left, Vector128<byte> topLeft)
        {
            // The Paeth distances to left and top simplify to |top - topLeft| and |left - topLeft|. The established
            // PNG predictor uses the same byte-lane identity, avoiding four widening stages for every sixteen samples.
            Vector128<byte> topMinusTopLeft = Vector128.SubtractSaturate(top, topLeft);
            Vector128<byte> leftMinusTopLeft = Vector128.SubtractSaturate(left, topLeft);
            Vector128<byte> distanceLeft = Vector128.SubtractSaturate(topLeft, top) | topMinusTopLeft;
            Vector128<byte> distanceTop = Vector128.SubtractSaturate(topLeft, left) | leftMinusTopLeft;
            Vector128<byte> sameDirection = Vector128.Equals(Vector128.Equals(topMinusTopLeft, Vector128<byte>.Zero), Vector128.Equals(leftMinusTopLeft, Vector128<byte>.Zero));
            Vector128<byte> distanceTopLeft = sameDirection | Vector128.SubtractSaturate(distanceTop, distanceLeft) | Vector128.SubtractSaturate(distanceLeft, distanceTop);
            Vector128<byte> minimumTopTopLeft = Vector128.Min(distanceTopLeft, distanceTop);
            Vector128<byte> topOrTopLeft = Vector128.ConditionalSelect(Vector128.Equals(minimumTopTopLeft, distanceTop), top, topLeft);
            return Vector128.ConditionalSelect(Vector128.Equals(Vector128.Min(minimumTopTopLeft, distanceLeft), distanceLeft), left, topOrTopLeft);
        }

        /// <summary>
        /// Selects the nearest Paeth reference for thirty-two 8-bit lanes.
        /// </summary>
        /// <param name="top">The top candidates.</param>
        /// <param name="left">The left candidates.</param>
        /// <param name="topLeft">The top-left candidates.</param>
        /// <returns>The selected candidates.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<byte> PredictPaeth(Vector256<byte> top, Vector256<byte> left, Vector256<byte> topLeft)
        {
            Vector256<byte> topMinusTopLeft = Vector256.SubtractSaturate(top, topLeft);
            Vector256<byte> leftMinusTopLeft = Vector256.SubtractSaturate(left, topLeft);
            Vector256<byte> distanceLeft = Vector256.SubtractSaturate(topLeft, top) | topMinusTopLeft;
            Vector256<byte> distanceTop = Vector256.SubtractSaturate(topLeft, left) | leftMinusTopLeft;
            Vector256<byte> sameDirection = Vector256.Equals(Vector256.Equals(topMinusTopLeft, Vector256<byte>.Zero), Vector256.Equals(leftMinusTopLeft, Vector256<byte>.Zero));
            Vector256<byte> distanceTopLeft = sameDirection | Vector256.SubtractSaturate(distanceTop, distanceLeft) | Vector256.SubtractSaturate(distanceLeft, distanceTop);
            Vector256<byte> minimumTopTopLeft = Vector256.Min(distanceTopLeft, distanceTop);
            Vector256<byte> topOrTopLeft = Vector256.ConditionalSelect(Vector256.Equals(minimumTopTopLeft, distanceTop), top, topLeft);
            return Vector256.ConditionalSelect(Vector256.Equals(Vector256.Min(minimumTopTopLeft, distanceLeft), distanceLeft), left, topOrTopLeft);
        }

        /// <summary>
        /// Selects the nearest Paeth reference for sixty-four 8-bit lanes.
        /// </summary>
        /// <param name="top">The top candidates.</param>
        /// <param name="left">The left candidates.</param>
        /// <param name="topLeft">The top-left candidates.</param>
        /// <returns>The selected candidates.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<byte> PredictPaeth(Vector512<byte> top, Vector512<byte> left, Vector512<byte> topLeft)
        {
            Vector512<byte> topMinusTopLeft = Vector512.SubtractSaturate(top, topLeft);
            Vector512<byte> leftMinusTopLeft = Vector512.SubtractSaturate(left, topLeft);
            Vector512<byte> distanceLeft = Vector512.SubtractSaturate(topLeft, top) | topMinusTopLeft;
            Vector512<byte> distanceTop = Vector512.SubtractSaturate(topLeft, left) | leftMinusTopLeft;
            Vector512<byte> sameDirection = Vector512.Equals(Vector512.Equals(topMinusTopLeft, Vector512<byte>.Zero), Vector512.Equals(leftMinusTopLeft, Vector512<byte>.Zero));
            Vector512<byte> distanceTopLeft = sameDirection | Vector512.SubtractSaturate(distanceTop, distanceLeft) | Vector512.SubtractSaturate(distanceLeft, distanceTop);
            Vector512<byte> minimumTopTopLeft = Vector512.Min(distanceTopLeft, distanceTop);
            Vector512<byte> topOrTopLeft = Vector512.ConditionalSelect(Vector512.Equals(minimumTopTopLeft, distanceTop), top, topLeft);
            return Vector512.ConditionalSelect(Vector512.Equals(Vector512.Min(minimumTopTopLeft, distanceLeft), distanceLeft), left, topOrTopLeft);
        }

        /// <summary>
        /// Selects the nearest Paeth reference for eight high-bit-depth lanes.
        /// </summary>
        /// <param name="top">The top candidates.</param>
        /// <param name="left">The left candidates.</param>
        /// <param name="topLeft">The top-left candidates.</param>
        /// <returns>The selected candidates.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<short> PredictPaeth(Vector128<short> top, Vector128<short> left, Vector128<short> topLeft)
        {
            // AV1 samples are at most twelve bits. Both signed differences and their sum are therefore bounded by
            // 8190, allowing the exact Paeth distances to stay in signed 16-bit lanes without widening.
            Vector128<short> topDelta = top - topLeft;
            Vector128<short> leftDelta = left - topLeft;
            Vector128<short> distanceLeft = Vector128.Abs(topDelta);
            Vector128<short> distanceTop = Vector128.Abs(leftDelta);
            Vector128<short> distanceTopLeft = Vector128.Abs(topDelta + leftDelta);

            Vector128<short> minimumTopTopLeft = Vector128.Min(distanceTopLeft, distanceTop);
            Vector128<short> topOrTopLeft = Vector128.ConditionalSelect(Vector128.Equals(minimumTopTopLeft, distanceTop), top, topLeft);
            return Vector128.ConditionalSelect(Vector128.Equals(Vector128.Min(minimumTopTopLeft, distanceLeft), distanceLeft), left, topOrTopLeft);
        }

        /// <summary>
        /// Selects the nearest Paeth reference for sixteen high-bit-depth lanes.
        /// </summary>
        /// <param name="top">The top candidates.</param>
        /// <param name="left">The left candidates.</param>
        /// <param name="topLeft">The top-left candidates.</param>
        /// <returns>The selected candidates.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector256<short> PredictPaeth(Vector256<short> top, Vector256<short> left, Vector256<short> topLeft)
        {
            Vector256<short> topDelta = top - topLeft;
            Vector256<short> leftDelta = left - topLeft;
            Vector256<short> distanceLeft = Vector256.Abs(topDelta);
            Vector256<short> distanceTop = Vector256.Abs(leftDelta);
            Vector256<short> distanceTopLeft = Vector256.Abs(topDelta + leftDelta);

            Vector256<short> minimumTopTopLeft = Vector256.Min(distanceTopLeft, distanceTop);
            Vector256<short> topOrTopLeft = Vector256.ConditionalSelect(Vector256.Equals(minimumTopTopLeft, distanceTop), top, topLeft);
            return Vector256.ConditionalSelect(Vector256.Equals(Vector256.Min(minimumTopTopLeft, distanceLeft), distanceLeft), left, topOrTopLeft);
        }

        /// <summary>
        /// Selects the nearest Paeth reference for thirty-two high-bit-depth lanes.
        /// </summary>
        /// <param name="top">The top candidates.</param>
        /// <param name="left">The left candidates.</param>
        /// <param name="topLeft">The top-left candidates.</param>
        /// <returns>The selected candidates.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector512<short> PredictPaeth(Vector512<short> top, Vector512<short> left, Vector512<short> topLeft)
        {
            Vector512<short> topDelta = top - topLeft;
            Vector512<short> leftDelta = left - topLeft;
            Vector512<short> distanceLeft = Vector512.Abs(topDelta);
            Vector512<short> distanceTop = Vector512.Abs(leftDelta);
            Vector512<short> distanceTopLeft = Vector512.Abs(topDelta + leftDelta);

            Vector512<short> minimumTopTopLeft = Vector512.Min(distanceTopLeft, distanceTop);
            Vector512<short> topOrTopLeft = Vector512.ConditionalSelect(Vector512.Equals(minimumTopTopLeft, distanceTop), top, topLeft);
            return Vector512.ConditionalSelect(Vector512.Equals(Vector512.Min(minimumTopTopLeft, distanceLeft), distanceLeft), left, topOrTopLeft);
        }
    }
}
