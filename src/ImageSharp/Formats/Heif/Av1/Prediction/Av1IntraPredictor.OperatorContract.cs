// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal abstract partial class Av1IntraPredictorBase
{
    /// <summary>
    /// Identifies the neighboring inputs consumed by an AV1 intra-prediction operator.
    /// </summary>
    [Flags]
    internal enum Av1IntraPredictionInputs
    {
        /// <summary>
        /// The operator does not consume neighboring samples.
        /// </summary>
        None = 0,

        /// <summary>
        /// The operator consumes samples from the top reference.
        /// </summary>
        Top = 1,

        /// <summary>
        /// The operator consumes samples from the left reference.
        /// </summary>
        Left = 2,

        /// <summary>
        /// The operator consumes the shared top-left reference.
        /// </summary>
        TopLeft = 4,

        /// <summary>
        /// The operator consumes the final top reference.
        /// </summary>
        TopRight = 8,

        /// <summary>
        /// The operator consumes the final left reference.
        /// </summary>
        BottomLeft = 16,

        /// <summary>
        /// The operator consumes the horizontal smooth weights.
        /// </summary>
        ColumnWeight = 32,

        /// <summary>
        /// The operator consumes the vertical smooth weights.
        /// </summary>
        RowWeight = 64,
    }

    /// <summary>
    /// Defines the scalar and SIMD arithmetic for one non-directional AV1 intra-prediction mode.
    /// </summary>
    /// <remarks>
    /// Each overload performs the same lane-wise operation. The generic predictor traversal selects the widest
    /// available overload, and the JIT specializes each static interface call for the closed operator type.
    /// </remarks>
    internal interface IAv1IntraPredictionOperator
    {
        /// <summary>
        /// Gets the prediction mode implemented by the operator.
        /// </summary>
        public static abstract Av1PredictionMode Mode { get; }

        /// <summary>
        /// Gets the neighboring inputs consumed by the operator.
        /// </summary>
        public static abstract Av1IntraPredictionInputs Inputs { get; }

        /// <summary>
        /// Predicts one 8-bit sample when hardware vectorization is unavailable.
        /// </summary>
        /// <param name="top">The top reference sample.</param>
        /// <param name="left">The left reference sample.</param>
        /// <param name="topLeft">The shared top-left reference sample.</param>
        /// <param name="topRight">The final top reference sample.</param>
        /// <param name="bottomLeft">The final left reference sample.</param>
        /// <param name="columnWeight">The horizontal Q8 smooth weight.</param>
        /// <param name="rowWeight">The vertical Q8 smooth weight.</param>
        /// <returns>The predicted sample.</returns>
        public static abstract byte Predict(byte top, byte left, byte topLeft, byte topRight, byte bottomLeft, int columnWeight, int rowWeight);

        /// <summary>
        /// Predicts sixteen 8-bit samples in parallel.
        /// </summary>
        /// <param name="top">The top reference samples.</param>
        /// <param name="left">The left reference sample in every lane.</param>
        /// <param name="topLeft">The shared top-left reference sample in every lane.</param>
        /// <param name="topRight">The final top reference sample in every lane.</param>
        /// <param name="bottomLeft">The final left reference sample in every lane.</param>
        /// <param name="columnWeights">The first horizontal Q8 smooth weight for these lanes.</param>
        /// <param name="rowWeight">The vertical Q8 smooth weight.</param>
        /// <returns>The predicted samples.</returns>
        public static abstract Vector128<byte> Predict(Vector128<byte> top, Vector128<byte> left, Vector128<byte> topLeft, Vector128<byte> topRight, Vector128<byte> bottomLeft, ref int columnWeights, int rowWeight);

        /// <summary>
        /// Predicts thirty-two 8-bit samples in parallel.
        /// </summary>
        /// <param name="top">The top reference samples.</param>
        /// <param name="left">The left reference sample in every lane.</param>
        /// <param name="topLeft">The shared top-left reference sample in every lane.</param>
        /// <param name="topRight">The final top reference sample in every lane.</param>
        /// <param name="bottomLeft">The final left reference sample in every lane.</param>
        /// <param name="columnWeights">The first horizontal Q8 smooth weight for these lanes.</param>
        /// <param name="rowWeight">The vertical Q8 smooth weight.</param>
        /// <returns>The predicted samples.</returns>
        public static abstract Vector256<byte> Predict(Vector256<byte> top, Vector256<byte> left, Vector256<byte> topLeft, Vector256<byte> topRight, Vector256<byte> bottomLeft, ref int columnWeights, int rowWeight);

        /// <summary>
        /// Predicts sixty-four 8-bit samples in parallel.
        /// </summary>
        /// <param name="top">The top reference samples.</param>
        /// <param name="left">The left reference sample in every lane.</param>
        /// <param name="topLeft">The shared top-left reference sample in every lane.</param>
        /// <param name="topRight">The final top reference sample in every lane.</param>
        /// <param name="bottomLeft">The final left reference sample in every lane.</param>
        /// <param name="columnWeights">The first horizontal Q8 smooth weight for these lanes.</param>
        /// <param name="rowWeight">The vertical Q8 smooth weight.</param>
        /// <returns>The predicted samples.</returns>
        public static abstract Vector512<byte> Predict(Vector512<byte> top, Vector512<byte> left, Vector512<byte> topLeft, Vector512<byte> topRight, Vector512<byte> bottomLeft, ref int columnWeights, int rowWeight);

        /// <summary>
        /// Predicts one high-bit-depth sample when hardware vectorization is unavailable.
        /// </summary>
        /// <param name="top">The top reference sample.</param>
        /// <param name="left">The left reference sample.</param>
        /// <param name="topLeft">The shared top-left reference sample.</param>
        /// <param name="topRight">The final top reference sample.</param>
        /// <param name="bottomLeft">The final left reference sample.</param>
        /// <param name="columnWeight">The horizontal Q8 smooth weight.</param>
        /// <param name="rowWeight">The vertical Q8 smooth weight.</param>
        /// <returns>The predicted sample.</returns>
        public static abstract short Predict(short top, short left, short topLeft, short topRight, short bottomLeft, int columnWeight, int rowWeight);

        /// <summary>
        /// Predicts eight high-bit-depth samples in parallel.
        /// </summary>
        /// <param name="top">The top reference samples.</param>
        /// <param name="left">The left reference sample in every lane.</param>
        /// <param name="topLeft">The shared top-left reference sample in every lane.</param>
        /// <param name="topRight">The final top reference sample in every lane.</param>
        /// <param name="bottomLeft">The final left reference sample in every lane.</param>
        /// <param name="columnWeights">The first horizontal Q8 smooth weight for these lanes.</param>
        /// <param name="rowWeight">The vertical Q8 smooth weight.</param>
        /// <returns>The predicted samples.</returns>
        public static abstract Vector128<short> Predict(Vector128<short> top, Vector128<short> left, Vector128<short> topLeft, Vector128<short> topRight, Vector128<short> bottomLeft, ref int columnWeights, int rowWeight);

        /// <summary>
        /// Predicts sixteen high-bit-depth samples in parallel.
        /// </summary>
        /// <param name="top">The top reference samples.</param>
        /// <param name="left">The left reference sample in every lane.</param>
        /// <param name="topLeft">The shared top-left reference sample in every lane.</param>
        /// <param name="topRight">The final top reference sample in every lane.</param>
        /// <param name="bottomLeft">The final left reference sample in every lane.</param>
        /// <param name="columnWeights">The first horizontal Q8 smooth weight for these lanes.</param>
        /// <param name="rowWeight">The vertical Q8 smooth weight.</param>
        /// <returns>The predicted samples.</returns>
        public static abstract Vector256<short> Predict(Vector256<short> top, Vector256<short> left, Vector256<short> topLeft, Vector256<short> topRight, Vector256<short> bottomLeft, ref int columnWeights, int rowWeight);

        /// <summary>
        /// Predicts thirty-two high-bit-depth samples in parallel.
        /// </summary>
        /// <param name="top">The top reference samples.</param>
        /// <param name="left">The left reference sample in every lane.</param>
        /// <param name="topLeft">The shared top-left reference sample in every lane.</param>
        /// <param name="topRight">The final top reference sample in every lane.</param>
        /// <param name="bottomLeft">The final left reference sample in every lane.</param>
        /// <param name="columnWeights">The first horizontal Q8 smooth weight for these lanes.</param>
        /// <param name="rowWeight">The vertical Q8 smooth weight.</param>
        /// <returns>The predicted samples.</returns>
        public static abstract Vector512<short> Predict(Vector512<short> top, Vector512<short> left, Vector512<short> topLeft, Vector512<short> topRight, Vector512<short> bottomLeft, ref int columnWeights, int rowWeight);
    }
}
