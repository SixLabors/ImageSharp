// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

/// <content>
/// Defines the neighbor-usage flags and scalar/SIMD contract for closed intra-prediction operators, and provides
/// their shared width-progressive SIMD traversal.
/// </content>
internal abstract partial class Av1NonDirectionalIntraPredictorBase
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

    /// <summary>
    /// Applies one closed non-directional AV1 prediction operator using the widest available SIMD width.
    /// </summary>
    /// <typeparam name="TOperator">The prediction-mode-specific arithmetic.</typeparam>
    /// <remarks>
    /// Each lane produces one output column. Top samples and column weights vary by lane, while the current row's
    /// left sample and row weight are broadcast. <typeparamref name="TOperator"/> declares which references it uses;
    /// because the operator type is closed, the JIT can remove unused loads and broadcasts from each prediction mode.
    /// </remarks>
    internal sealed class Av1NonDirectionalIntraPredictor<TOperator> : Av1NonDirectionalIntraPredictorBase
        where TOperator : struct, IAv1IntraPredictionOperator
    {
        /// <inheritdoc/>
        public override Av1PredictionMode Mode => TOperator.Mode;

        /// <inheritdoc/>
        public override void Predict(Span<byte> destination, int destinationStride, ReadOnlySpan<byte> above, ReadOnlySpan<byte> left, int width, int height)
        {
            ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
            ref byte topBase = ref MemoryMarshal.GetReference(above);
            ref byte leftBase = ref MemoryMarshal.GetReference(left);
            ref int columnWeightBase = ref MemoryMarshal.GetReference(SmoothWeights[width..]);

            Av1IntraPredictionInputs inputs = TOperator.Inputs;
            bool usesTop = (inputs & Av1IntraPredictionInputs.Top) != 0;
            bool usesLeft = (inputs & Av1IntraPredictionInputs.Left) != 0;
            bool usesTopLeft = (inputs & Av1IntraPredictionInputs.TopLeft) != 0;
            bool usesTopRight = (inputs & Av1IntraPredictionInputs.TopRight) != 0;
            bool usesBottomLeft = (inputs & Av1IntraPredictionInputs.BottomLeft) != 0;
            bool usesColumnWeight = (inputs & Av1IntraPredictionInputs.ColumnWeight) != 0;
            bool usesRowWeight = (inputs & Av1IntraPredictionInputs.RowWeight) != 0;

            byte topLeft = usesTopLeft ? Unsafe.Subtract(ref topBase, 1) : default;
            byte topRight = usesTopRight ? Unsafe.Add(ref topBase, width - 1) : default;
            byte bottomLeft = usesBottomLeft ? Unsafe.Add(ref leftBase, height - 1) : default;
            int processedColumns = 0;

            // Widths are cumulative rather than mutually exclusive. A wide vector advances the row prefix, then the
            // narrower paths consume any complete vectors left before the scalar tail handles the final columns.
            if (Vector512.IsHardwareAccelerated)
            {
                int vectorizedColumns = width - (width % Vector512<byte>.Count);
                if (vectorizedColumns > 0)
                {
                    Vector512<byte> topLeftVector = usesTopLeft ? Vector512.Create(topLeft) : default;
                    Vector512<byte> topRightVector = usesTopRight ? Vector512.Create(topRight) : default;
                    Vector512<byte> bottomLeftVector = usesBottomLeft ? Vector512.Create(bottomLeft) : default;

                    for (int row = 0; row < height; row++)
                    {
                        Vector512<byte> leftVector = usesLeft ? Vector512.Create(Unsafe.Add(ref leftBase, row)) : default;
                        int rowWeight = usesRowWeight ? SmoothWeights[height + row] : 0;
                        ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

                        for (int column = 0; column < vectorizedColumns; column += Vector512<byte>.Count)
                        {
                            Vector512<byte> top = usesTop ? Vector512.LoadUnsafe(ref topBase, (nuint)column) : default;
                            ref int columnWeights = ref usesColumnWeight ? ref Unsafe.Add(ref columnWeightBase, column) : ref columnWeightBase;
                            Vector512<byte> prediction = TOperator.Predict(top, leftVector, topLeftVector, topRightVector, bottomLeftVector, ref columnWeights, rowWeight);
                            prediction.StoreUnsafe(ref destinationRow, (nuint)column);
                        }
                    }

                    processedColumns = vectorizedColumns;
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int remainingColumns = width - processedColumns;
                int vectorizedColumns = remainingColumns - (remainingColumns % Vector256<byte>.Count);
                if (vectorizedColumns > 0)
                {
                    Vector256<byte> topLeftVector = usesTopLeft ? Vector256.Create(topLeft) : default;
                    Vector256<byte> topRightVector = usesTopRight ? Vector256.Create(topRight) : default;
                    Vector256<byte> bottomLeftVector = usesBottomLeft ? Vector256.Create(bottomLeft) : default;
                    int endColumn = processedColumns + vectorizedColumns;

                    for (int row = 0; row < height; row++)
                    {
                        Vector256<byte> leftVector = usesLeft ? Vector256.Create(Unsafe.Add(ref leftBase, row)) : default;
                        int rowWeight = usesRowWeight ? SmoothWeights[height + row] : 0;
                        ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

                        for (int column = processedColumns; column < endColumn; column += Vector256<byte>.Count)
                        {
                            Vector256<byte> top = usesTop ? Vector256.LoadUnsafe(ref topBase, (nuint)column) : default;
                            ref int columnWeights = ref usesColumnWeight ? ref Unsafe.Add(ref columnWeightBase, column) : ref columnWeightBase;
                            Vector256<byte> prediction = TOperator.Predict(top, leftVector, topLeftVector, topRightVector, bottomLeftVector, ref columnWeights, rowWeight);
                            prediction.StoreUnsafe(ref destinationRow, (nuint)column);
                        }
                    }

                    processedColumns = endColumn;
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                int remainingColumns = width - processedColumns;
                int vectorizedColumns = remainingColumns - (remainingColumns % Vector128<byte>.Count);
                if (vectorizedColumns > 0)
                {
                    Vector128<byte> topLeftVector = usesTopLeft ? Vector128.Create(topLeft) : default;
                    Vector128<byte> topRightVector = usesTopRight ? Vector128.Create(topRight) : default;
                    Vector128<byte> bottomLeftVector = usesBottomLeft ? Vector128.Create(bottomLeft) : default;
                    int endColumn = processedColumns + vectorizedColumns;

                    for (int row = 0; row < height; row++)
                    {
                        Vector128<byte> leftVector = usesLeft ? Vector128.Create(Unsafe.Add(ref leftBase, row)) : default;
                        int rowWeight = usesRowWeight ? SmoothWeights[height + row] : 0;
                        ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

                        for (int column = processedColumns; column < endColumn; column += Vector128<byte>.Count)
                        {
                            Vector128<byte> top = usesTop ? Vector128.LoadUnsafe(ref topBase, (nuint)column) : default;
                            ref int columnWeights = ref usesColumnWeight ? ref Unsafe.Add(ref columnWeightBase, column) : ref columnWeightBase;
                            Vector128<byte> prediction = TOperator.Predict(top, leftVector, topLeftVector, topRightVector, bottomLeftVector, ref columnWeights, rowWeight);
                            prediction.StoreUnsafe(ref destinationRow, (nuint)column);
                        }
                    }

                    processedColumns = endColumn;
                }
            }

            // AV1 dimensions are multiples of four. The tail is normally zero on SIMD hardware, but retaining the
            // scalar continuation keeps the traversal correct when intrinsics are disabled by FeatureTestRunner.
            for (int row = 0; row < height; row++)
            {
                byte leftSample = usesLeft ? Unsafe.Add(ref leftBase, row) : default;
                int rowWeight = usesRowWeight ? SmoothWeights[height + row] : 0;
                ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

                for (int column = processedColumns; column < width; column++)
                {
                    byte top = usesTop ? Unsafe.Add(ref topBase, column) : default;
                    int columnWeight = usesColumnWeight ? Unsafe.Add(ref columnWeightBase, column) : 0;
                    Unsafe.Add(ref destinationRow, column) = TOperator.Predict(top, leftSample, topLeft, topRight, bottomLeft, columnWeight, rowWeight);
                }
            }
        }

        /// <inheritdoc/>
        public override void Predict(Span<short> destination, int destinationStride, ReadOnlySpan<short> above, ReadOnlySpan<short> left, int width, int height)
        {
            ref short destinationBase = ref MemoryMarshal.GetReference(destination);
            ref short topBase = ref MemoryMarshal.GetReference(above);
            ref short leftBase = ref MemoryMarshal.GetReference(left);
            ref int columnWeightBase = ref MemoryMarshal.GetReference(SmoothWeights[width..]);

            Av1IntraPredictionInputs inputs = TOperator.Inputs;
            bool usesTop = (inputs & Av1IntraPredictionInputs.Top) != 0;
            bool usesLeft = (inputs & Av1IntraPredictionInputs.Left) != 0;
            bool usesTopLeft = (inputs & Av1IntraPredictionInputs.TopLeft) != 0;
            bool usesTopRight = (inputs & Av1IntraPredictionInputs.TopRight) != 0;
            bool usesBottomLeft = (inputs & Av1IntraPredictionInputs.BottomLeft) != 0;
            bool usesColumnWeight = (inputs & Av1IntraPredictionInputs.ColumnWeight) != 0;
            bool usesRowWeight = (inputs & Av1IntraPredictionInputs.RowWeight) != 0;

            short topLeft = usesTopLeft ? Unsafe.Subtract(ref topBase, 1) : default;
            short topRight = usesTopRight ? Unsafe.Add(ref topBase, width - 1) : default;
            short bottomLeft = usesBottomLeft ? Unsafe.Add(ref leftBase, height - 1) : default;
            int processedColumns = 0;

            // High-bit-depth samples use signed storage but remain nonnegative. Each vector lane follows one output
            // column, so the same width-progressive traversal is valid without inter-lane packing or saturation.
            if (Vector512.IsHardwareAccelerated)
            {
                int vectorizedColumns = width - (width % Vector512<short>.Count);
                if (vectorizedColumns > 0)
                {
                    Vector512<short> topLeftVector = usesTopLeft ? Vector512.Create(topLeft) : default;
                    Vector512<short> topRightVector = usesTopRight ? Vector512.Create(topRight) : default;
                    Vector512<short> bottomLeftVector = usesBottomLeft ? Vector512.Create(bottomLeft) : default;

                    for (int row = 0; row < height; row++)
                    {
                        Vector512<short> leftVector = usesLeft ? Vector512.Create(Unsafe.Add(ref leftBase, row)) : default;
                        int rowWeight = usesRowWeight ? SmoothWeights[height + row] : 0;
                        ref short destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

                        for (int column = 0; column < vectorizedColumns; column += Vector512<short>.Count)
                        {
                            Vector512<short> top = usesTop ? Vector512.LoadUnsafe(ref topBase, (nuint)column) : default;
                            ref int columnWeights = ref usesColumnWeight ? ref Unsafe.Add(ref columnWeightBase, column) : ref columnWeightBase;
                            Vector512<short> prediction = TOperator.Predict(top, leftVector, topLeftVector, topRightVector, bottomLeftVector, ref columnWeights, rowWeight);
                            prediction.StoreUnsafe(ref destinationRow, (nuint)column);
                        }
                    }

                    processedColumns = vectorizedColumns;
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int remainingColumns = width - processedColumns;
                int vectorizedColumns = remainingColumns - (remainingColumns % Vector256<short>.Count);
                if (vectorizedColumns > 0)
                {
                    Vector256<short> topLeftVector = usesTopLeft ? Vector256.Create(topLeft) : default;
                    Vector256<short> topRightVector = usesTopRight ? Vector256.Create(topRight) : default;
                    Vector256<short> bottomLeftVector = usesBottomLeft ? Vector256.Create(bottomLeft) : default;
                    int endColumn = processedColumns + vectorizedColumns;

                    for (int row = 0; row < height; row++)
                    {
                        Vector256<short> leftVector = usesLeft ? Vector256.Create(Unsafe.Add(ref leftBase, row)) : default;
                        int rowWeight = usesRowWeight ? SmoothWeights[height + row] : 0;
                        ref short destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

                        for (int column = processedColumns; column < endColumn; column += Vector256<short>.Count)
                        {
                            Vector256<short> top = usesTop ? Vector256.LoadUnsafe(ref topBase, (nuint)column) : default;
                            ref int columnWeights = ref usesColumnWeight ? ref Unsafe.Add(ref columnWeightBase, column) : ref columnWeightBase;
                            Vector256<short> prediction = TOperator.Predict(top, leftVector, topLeftVector, topRightVector, bottomLeftVector, ref columnWeights, rowWeight);
                            prediction.StoreUnsafe(ref destinationRow, (nuint)column);
                        }
                    }

                    processedColumns = endColumn;
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                int remainingColumns = width - processedColumns;
                int vectorizedColumns = remainingColumns - (remainingColumns % Vector128<short>.Count);
                if (vectorizedColumns > 0)
                {
                    Vector128<short> topLeftVector = usesTopLeft ? Vector128.Create(topLeft) : default;
                    Vector128<short> topRightVector = usesTopRight ? Vector128.Create(topRight) : default;
                    Vector128<short> bottomLeftVector = usesBottomLeft ? Vector128.Create(bottomLeft) : default;
                    int endColumn = processedColumns + vectorizedColumns;

                    for (int row = 0; row < height; row++)
                    {
                        Vector128<short> leftVector = usesLeft ? Vector128.Create(Unsafe.Add(ref leftBase, row)) : default;
                        int rowWeight = usesRowWeight ? SmoothWeights[height + row] : 0;
                        ref short destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

                        for (int column = processedColumns; column < endColumn; column += Vector128<short>.Count)
                        {
                            Vector128<short> top = usesTop ? Vector128.LoadUnsafe(ref topBase, (nuint)column) : default;
                            ref int columnWeights = ref usesColumnWeight ? ref Unsafe.Add(ref columnWeightBase, column) : ref columnWeightBase;
                            Vector128<short> prediction = TOperator.Predict(top, leftVector, topLeftVector, topRightVector, bottomLeftVector, ref columnWeights, rowWeight);
                            prediction.StoreUnsafe(ref destinationRow, (nuint)column);
                        }
                    }

                    processedColumns = endColumn;
                }
            }

            // Retain a scalar continuation for widths smaller than the available vectors and for forced-scalar test
            // execution. AV1 block dimensions keep this tail short during normal hardware-accelerated decoding.
            for (int row = 0; row < height; row++)
            {
                short leftSample = usesLeft ? Unsafe.Add(ref leftBase, row) : default;
                int rowWeight = usesRowWeight ? SmoothWeights[height + row] : 0;
                ref short destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

                for (int column = processedColumns; column < width; column++)
                {
                    short top = usesTop ? Unsafe.Add(ref topBase, column) : default;
                    int columnWeight = usesColumnWeight ? Unsafe.Add(ref columnWeightBase, column) : 0;
                    Unsafe.Add(ref destinationRow, column) = TOperator.Predict(top, leftSample, topLeft, topRight, bottomLeft, columnWeight, rowWeight);
                }
            }
        }

        /// <inheritdoc/>
        public override void PredictScalar(Span<byte> destination, int destinationStride, ReadOnlySpan<byte> above, ReadOnlySpan<byte> left, int width, int height)
        {
            ref byte destinationBase = ref MemoryMarshal.GetReference(destination);
            ref byte topBase = ref MemoryMarshal.GetReference(above);
            ref byte leftBase = ref MemoryMarshal.GetReference(left);
            ref int columnWeightBase = ref MemoryMarshal.GetReference(SmoothWeights[width..]);

            Av1IntraPredictionInputs inputs = TOperator.Inputs;
            bool usesTop = (inputs & Av1IntraPredictionInputs.Top) != 0;
            bool usesLeft = (inputs & Av1IntraPredictionInputs.Left) != 0;
            bool usesTopLeft = (inputs & Av1IntraPredictionInputs.TopLeft) != 0;
            bool usesTopRight = (inputs & Av1IntraPredictionInputs.TopRight) != 0;
            bool usesBottomLeft = (inputs & Av1IntraPredictionInputs.BottomLeft) != 0;
            bool usesColumnWeight = (inputs & Av1IntraPredictionInputs.ColumnWeight) != 0;
            bool usesRowWeight = (inputs & Av1IntraPredictionInputs.RowWeight) != 0;
            byte topLeft = usesTopLeft ? Unsafe.Subtract(ref topBase, 1) : default;
            byte topRight = usesTopRight ? Unsafe.Add(ref topBase, width - 1) : default;
            byte bottomLeft = usesBottomLeft ? Unsafe.Add(ref leftBase, height - 1) : default;

            for (int row = 0; row < height; row++)
            {
                byte leftSample = usesLeft ? Unsafe.Add(ref leftBase, row) : default;
                int rowWeight = usesRowWeight ? SmoothWeights[height + row] : 0;
                ref byte destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

                for (int column = 0; column < width; column++)
                {
                    byte top = usesTop ? Unsafe.Add(ref topBase, column) : default;
                    int columnWeight = usesColumnWeight ? Unsafe.Add(ref columnWeightBase, column) : 0;
                    Unsafe.Add(ref destinationRow, column) = TOperator.Predict(top, leftSample, topLeft, topRight, bottomLeft, columnWeight, rowWeight);
                }
            }
        }

        /// <inheritdoc/>
        public override void PredictScalar(Span<short> destination, int destinationStride, ReadOnlySpan<short> above, ReadOnlySpan<short> left, int width, int height)
        {
            ref short destinationBase = ref MemoryMarshal.GetReference(destination);
            ref short topBase = ref MemoryMarshal.GetReference(above);
            ref short leftBase = ref MemoryMarshal.GetReference(left);
            ref int columnWeightBase = ref MemoryMarshal.GetReference(SmoothWeights[width..]);

            Av1IntraPredictionInputs inputs = TOperator.Inputs;
            bool usesTop = (inputs & Av1IntraPredictionInputs.Top) != 0;
            bool usesLeft = (inputs & Av1IntraPredictionInputs.Left) != 0;
            bool usesTopLeft = (inputs & Av1IntraPredictionInputs.TopLeft) != 0;
            bool usesTopRight = (inputs & Av1IntraPredictionInputs.TopRight) != 0;
            bool usesBottomLeft = (inputs & Av1IntraPredictionInputs.BottomLeft) != 0;
            bool usesColumnWeight = (inputs & Av1IntraPredictionInputs.ColumnWeight) != 0;
            bool usesRowWeight = (inputs & Av1IntraPredictionInputs.RowWeight) != 0;
            short topLeft = usesTopLeft ? Unsafe.Subtract(ref topBase, 1) : default;
            short topRight = usesTopRight ? Unsafe.Add(ref topBase, width - 1) : default;
            short bottomLeft = usesBottomLeft ? Unsafe.Add(ref leftBase, height - 1) : default;

            for (int row = 0; row < height; row++)
            {
                short leftSample = usesLeft ? Unsafe.Add(ref leftBase, row) : default;
                int rowWeight = usesRowWeight ? SmoothWeights[height + row] : 0;
                ref short destinationRow = ref Unsafe.Add(ref destinationBase, row * destinationStride);

                for (int column = 0; column < width; column++)
                {
                    short top = usesTop ? Unsafe.Add(ref topBase, column) : default;
                    int columnWeight = usesColumnWeight ? Unsafe.Add(ref columnWeightBase, column) : 0;
                    Unsafe.Add(ref destinationRow, column) = TOperator.Predict(top, leftSample, topLeft, topRight, bottomLeft, columnWeight, rowWeight);
                }
            }
        }
    }
}
