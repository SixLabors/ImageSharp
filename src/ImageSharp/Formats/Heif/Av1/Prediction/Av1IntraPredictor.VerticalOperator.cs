// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal abstract partial class Av1IntraPredictorBase
{
    /// <summary>
    /// Implements AV1 vertical intra prediction for scalar and SIMD lanes.
    /// </summary>
    internal readonly struct VerticalOperator : IAv1IntraPredictionOperator
    {
        /// <inheritdoc/>
        public static Av1PredictionMode Mode => Av1PredictionMode.Vertical;

        /// <inheritdoc/>
        public static Av1IntraPredictionInputs Inputs => Av1IntraPredictionInputs.Top;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte Predict(byte top, byte left, byte topLeft, byte topRight, byte bottomLeft, int columnWeight, int rowWeight) => top;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<byte> Predict(Vector128<byte> top, Vector128<byte> left, Vector128<byte> topLeft, Vector128<byte> topRight, Vector128<byte> bottomLeft, ref int columnWeights, int rowWeight) => top;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<byte> Predict(Vector256<byte> top, Vector256<byte> left, Vector256<byte> topLeft, Vector256<byte> topRight, Vector256<byte> bottomLeft, ref int columnWeights, int rowWeight) => top;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<byte> Predict(Vector512<byte> top, Vector512<byte> left, Vector512<byte> topLeft, Vector512<byte> topRight, Vector512<byte> bottomLeft, ref int columnWeights, int rowWeight) => top;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short Predict(short top, short left, short topLeft, short topRight, short bottomLeft, int columnWeight, int rowWeight) => top;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<short> Predict(Vector128<short> top, Vector128<short> left, Vector128<short> topLeft, Vector128<short> topRight, Vector128<short> bottomLeft, ref int columnWeights, int rowWeight) => top;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<short> Predict(Vector256<short> top, Vector256<short> left, Vector256<short> topLeft, Vector256<short> topRight, Vector256<short> bottomLeft, ref int columnWeights, int rowWeight) => top;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<short> Predict(Vector512<short> top, Vector512<short> left, Vector512<short> topLeft, Vector512<short> topRight, Vector512<short> bottomLeft, ref int columnWeights, int rowWeight) => top;
    }
}
