// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

internal abstract partial class Av1IntraPredictorBase
{
    /// <summary>
    /// Applies one closed non-directional AV1 prediction operator using the widest available SIMD width.
    /// </summary>
    /// <typeparam name="TOperator">The prediction-mode-specific arithmetic.</typeparam>
    internal sealed class Av1IntraPredictor<TOperator> : Av1IntraPredictorBase
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
