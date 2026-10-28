// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

/// <content>
/// Binds shared inverse-transform columns to the existing sample reconstruction operators.
/// </content>
internal static partial class Av1Inverse2dTransformer
{
    /// <summary>
    /// Borrows prediction and destination storage for one inverse transform.
    /// </summary>
    /// <remarks>
    /// Transform coefficients and SIMD stage vectors always contain Int32 values. Sample storage is needed only when a completed column group is reconstructed.
    /// Keeping that storage here lets byte and high-bit-depth output use the same transform kernel. Row strides count native samples; byte views preserve the
    /// original storage and ownership. The view lives only for the transform call and allocates no buffers.
    /// The small storage loops request inlining to avoid calls at the column-group boundary. Only addition and packing depend on the sample type;
    /// the shared coefficient transform stays outside those typed loops.
    /// </remarks>
    internal readonly ref struct ReconstructionOutput
    {
        private readonly ReadOnlySpan<byte> prediction;
        private readonly Span<byte> destination;
        private readonly int predictionStride;
        private readonly int destinationStride;
        private readonly bool highBitDepth;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReconstructionOutput"/> struct for byte sample storage.
        /// </summary>
        /// <param name="prediction">The predicted byte samples.</param>
        /// <param name="predictionStride">The prediction row stride in samples.</param>
        /// <param name="destination">The reconstructed byte samples.</param>
        /// <param name="destinationStride">The destination row stride in samples.</param>
        public ReconstructionOutput(ReadOnlySpan<byte> prediction, int predictionStride, Span<byte> destination, int destinationStride)
        {
            this.prediction = prediction;
            this.destination = destination;
            this.predictionStride = predictionStride;
            this.destinationStride = destinationStride;
            this.highBitDepth = false;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReconstructionOutput"/> struct for high-bit-depth sample storage.
        /// </summary>
        /// <param name="prediction">The predicted high-bit-depth samples.</param>
        /// <param name="predictionStride">The prediction row stride in samples.</param>
        /// <param name="destination">The reconstructed high-bit-depth samples.</param>
        /// <param name="destinationStride">The destination row stride in samples.</param>
        public ReconstructionOutput(ReadOnlySpan<short> prediction, int predictionStride, Span<short> destination, int destinationStride)
        {
            this.prediction = MemoryMarshal.AsBytes(prediction);
            this.destination = MemoryMarshal.AsBytes(destination);
            this.predictionStride = predictionStride;
            this.destinationStride = destinationStride;
            this.highBitDepth = true;
        }

        /// <summary>
        /// Reconstructs four completed columns with the existing native sample operator.
        /// </summary>
        /// <param name="residuals">The second-axis vectors. Each lane belongs to one column.</param>
        /// <param name="column">The destination column of the first lane.</param>
        /// <param name="height">The number of reconstructed rows.</param>
        /// <param name="shift">The signed rounding shift of the second transform axis.</param>
        /// <param name="flip">Whether reconstructed rows read residuals in reverse order.</param>
        /// <param name="bitDepth">The coded sample precision used by the output operator.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteColumns(ref Av1TransformVector<Vector128<int>> residuals, int column, int height, int shift, bool flip, int bitDepth)
        {
            // Choose storage once for the complete column group. Pass native references directly, so the call copies no span descriptors.
            // The row loop uses a concrete output operator, with no per-sample dispatch.
            if (this.highBitDepth)
            {
                WriteColumns<short, Av1InverseTransformer.HighBitDepthOutputOperator>(
                    ref Unsafe.As<byte, short>(ref MemoryMarshal.GetReference(this.prediction)),
                    this.predictionStride,
                    ref Unsafe.As<byte, short>(ref MemoryMarshal.GetReference(this.destination)),
                    this.destinationStride,
                    ref residuals,
                    column,
                    height,
                    shift,
                    flip,
                    bitDepth);
            }
            else
            {
                WriteColumns<byte, Av1InverseTransformer.ByteOutputOperator>(
                    ref MemoryMarshal.GetReference(this.prediction),
                    this.predictionStride,
                    ref MemoryMarshal.GetReference(this.destination),
                    this.destinationStride,
                    ref residuals,
                    column,
                    height,
                    shift,
                    flip,
                    bitDepth);
            }
        }

        /// <summary>
        /// Reconstructs eight completed columns with the existing native sample operator.
        /// </summary>
        /// <param name="residuals">The second-axis vectors. Each lane belongs to one column.</param>
        /// <param name="column">The destination column of the first lane.</param>
        /// <param name="height">The number of reconstructed rows.</param>
        /// <param name="shift">The signed rounding shift of the second transform axis.</param>
        /// <param name="flip">Whether reconstructed rows read residuals in reverse order.</param>
        /// <param name="bitDepth">The coded sample precision used by the output operator.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteColumns(ref Av1TransformVector<Vector256<int>> residuals, int column, int height, int shift, bool flip, int bitDepth)
        {
            // Choose storage once for the complete column group. The row loop then uses a concrete output operator, with no per-sample dispatch.
            if (this.highBitDepth)
            {
                WriteColumns<short, Av1InverseTransformer.HighBitDepthOutputOperator>(
                    ref Unsafe.As<byte, short>(ref MemoryMarshal.GetReference(this.prediction)),
                    this.predictionStride,
                    ref Unsafe.As<byte, short>(ref MemoryMarshal.GetReference(this.destination)),
                    this.destinationStride,
                    ref residuals,
                    column,
                    height,
                    shift,
                    flip,
                    bitDepth);
            }
            else
            {
                WriteColumns<byte, Av1InverseTransformer.ByteOutputOperator>(
                    ref MemoryMarshal.GetReference(this.prediction),
                    this.predictionStride,
                    ref MemoryMarshal.GetReference(this.destination),
                    this.destinationStride,
                    ref residuals,
                    column,
                    height,
                    shift,
                    flip,
                    bitDepth);
            }
        }

        /// <summary>
        /// Reconstructs sixteen completed columns with the existing native sample operator.
        /// </summary>
        /// <param name="residuals">The second-axis vectors. Each lane belongs to one column.</param>
        /// <param name="column">The destination column of the first lane.</param>
        /// <param name="height">The number of reconstructed rows.</param>
        /// <param name="shift">The signed rounding shift of the second transform axis.</param>
        /// <param name="flip">Whether reconstructed rows read residuals in reverse order.</param>
        /// <param name="bitDepth">The coded sample precision used by the output operator.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteColumns(ref Av1TransformVector<Vector512<int>> residuals, int column, int height, int shift, bool flip, int bitDepth)
        {
            // Choose storage once for the complete column group. The row loop then uses a concrete output operator, with no per-sample dispatch.
            if (this.highBitDepth)
            {
                WriteColumns<short, Av1InverseTransformer.HighBitDepthOutputOperator>(
                    ref Unsafe.As<byte, short>(ref MemoryMarshal.GetReference(this.prediction)),
                    this.predictionStride,
                    ref Unsafe.As<byte, short>(ref MemoryMarshal.GetReference(this.destination)),
                    this.destinationStride,
                    ref residuals,
                    column,
                    height,
                    shift,
                    flip,
                    bitDepth);
            }
            else
            {
                WriteColumns<byte, Av1InverseTransformer.ByteOutputOperator>(
                    ref MemoryMarshal.GetReference(this.prediction),
                    this.predictionStride,
                    ref MemoryMarshal.GetReference(this.destination),
                    this.destinationStride,
                    ref residuals,
                    column,
                    height,
                    shift,
                    flip,
                    bitDepth);
            }
        }

        /// <summary>
        /// Reconstructs one completed column when SIMD is unavailable.
        /// </summary>
        /// <param name="residuals">The second-axis values after their rounding shift.</param>
        /// <param name="column">The destination column.</param>
        /// <param name="height">The number of reconstructed rows.</param>
        /// <param name="flip">Whether reconstructed rows read residuals in reverse order.</param>
        /// <param name="bitDepth">The coded sample precision used by the output operator.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteColumn(ReadOnlySpan<int> residuals, int column, int height, bool flip, int bitDepth)
        {
            if (this.highBitDepth)
            {
                WriteColumn<short, Av1InverseTransformer.HighBitDepthOutputOperator>(
                    MemoryMarshal.Cast<byte, short>(this.prediction),
                    this.predictionStride,
                    MemoryMarshal.Cast<byte, short>(this.destination),
                    this.destinationStride,
                    residuals,
                    column,
                    height,
                    flip,
                    bitDepth);
            }
            else
            {
                WriteColumn<byte, Av1InverseTransformer.ByteOutputOperator>(
                    this.prediction, this.predictionStride, this.destination, this.destinationStride, residuals, column, height, flip, bitDepth);
            }
        }

        /// <summary>
        /// Adds, clips and stores four columns without changing their SIMD lane order.
        /// </summary>
        /// <typeparam name="TSample">The native prediction and destination sample type.</typeparam>
        /// <typeparam name="TOutputOperator">The existing addition, clipping and packing operator.</typeparam>
        /// <param name="prediction">The first sample of the original prediction plane.</param>
        /// <param name="predictionStride">The prediction row stride in native samples.</param>
        /// <param name="destination">The first sample of the original reconstruction plane.</param>
        /// <param name="destinationStride">The destination row stride in native samples.</param>
        /// <param name="residuals">The second-axis vectors, one independent column per lane.</param>
        /// <param name="column">The destination column of the first lane.</param>
        /// <param name="height">The number of reconstructed rows.</param>
        /// <param name="shift">The signed rounding shift of the second transform axis.</param>
        /// <param name="flip">Whether reconstructed rows read residuals in reverse order.</param>
        /// <param name="bitDepth">The coded sample precision used by the output operator.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteColumns<TSample, TOutputOperator>(
            ref TSample prediction,
            int predictionStride,
            ref TSample destination,
            int destinationStride,
            ref Av1TransformVector<Vector128<int>> residuals,
            int column,
            int height,
            int shift,
            bool flip,
            int bitDepth)
            where TSample : unmanaged
            where TOutputOperator : struct, Av1InverseTransformer.IAv1InverseTransformOutputOperator<TSample>
        {
            // The transform already applied horizontal flips while loading its columns. A vertical flip selects the completed residual row here. Each output
            // operation reads and writes exactly four samples, so packed row boundaries need no padding. Prediction and destination can share storage.
            for (int row = 0; row < height; row++)
            {
                int sourceRow = flip ? height - row - 1 : row;
                Vector128<int> residual = Av1Transform2dOperations.RoundShift(residuals[sourceRow], shift);
                ref TSample predicted = ref Unsafe.Add(ref prediction, (row * predictionStride) + column);
                ref TSample target = ref Unsafe.Add(ref destination, (row * destinationStride) + column);

                TOutputOperator.Add(ref predicted, ref target, residual, bitDepth);
            }
        }

        /// <summary>
        /// Adds, clips and stores eight columns without changing their SIMD lane order.
        /// </summary>
        /// <typeparam name="TSample">The native prediction and destination sample type.</typeparam>
        /// <typeparam name="TOutputOperator">The existing addition, clipping and packing operator.</typeparam>
        /// <param name="prediction">The first sample of the original prediction plane.</param>
        /// <param name="predictionStride">The prediction row stride in native samples.</param>
        /// <param name="destination">The first sample of the original reconstruction plane.</param>
        /// <param name="destinationStride">The destination row stride in native samples.</param>
        /// <param name="residuals">The second-axis vectors, one independent column per lane.</param>
        /// <param name="column">The destination column of the first lane.</param>
        /// <param name="height">The number of reconstructed rows.</param>
        /// <param name="shift">The signed rounding shift of the second transform axis.</param>
        /// <param name="flip">Whether reconstructed rows read residuals in reverse order.</param>
        /// <param name="bitDepth">The coded sample precision used by the output operator.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteColumns<TSample, TOutputOperator>(
            ref TSample prediction,
            int predictionStride,
            ref TSample destination,
            int destinationStride,
            ref Av1TransformVector<Vector256<int>> residuals,
            int column,
            int height,
            int shift,
            bool flip,
            int bitDepth)
            where TSample : unmanaged
            where TOutputOperator : struct, Av1InverseTransformer.IAv1InverseTransformOutputOperator<TSample>
        {
            // The transform already applied horizontal flips while loading its columns. A vertical flip selects the completed residual row here. Each output
            // operation reads and writes exactly eight samples, so packed row boundaries need no padding. Prediction and destination can share storage.
            for (int row = 0; row < height; row++)
            {
                int sourceRow = flip ? height - row - 1 : row;
                Vector256<int> residual = Av1Transform2dOperations.RoundShift(residuals[sourceRow], shift);
                ref TSample predicted = ref Unsafe.Add(ref prediction, (row * predictionStride) + column);
                ref TSample target = ref Unsafe.Add(ref destination, (row * destinationStride) + column);

                TOutputOperator.Add(ref predicted, ref target, residual, bitDepth);
            }
        }

        /// <summary>
        /// Adds, clips and stores sixteen columns without changing their SIMD lane order.
        /// </summary>
        /// <typeparam name="TSample">The native prediction and destination sample type.</typeparam>
        /// <typeparam name="TOutputOperator">The existing addition, clipping and packing operator.</typeparam>
        /// <param name="prediction">The first sample of the original prediction plane.</param>
        /// <param name="predictionStride">The prediction row stride in native samples.</param>
        /// <param name="destination">The first sample of the original reconstruction plane.</param>
        /// <param name="destinationStride">The destination row stride in native samples.</param>
        /// <param name="residuals">The second-axis vectors, one independent column per lane.</param>
        /// <param name="column">The destination column of the first lane.</param>
        /// <param name="height">The number of reconstructed rows.</param>
        /// <param name="shift">The signed rounding shift of the second transform axis.</param>
        /// <param name="flip">Whether reconstructed rows read residuals in reverse order.</param>
        /// <param name="bitDepth">The coded sample precision used by the output operator.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteColumns<TSample, TOutputOperator>(
            ref TSample prediction,
            int predictionStride,
            ref TSample destination,
            int destinationStride,
            ref Av1TransformVector<Vector512<int>> residuals,
            int column,
            int height,
            int shift,
            bool flip,
            int bitDepth)
            where TSample : unmanaged
            where TOutputOperator : struct, Av1InverseTransformer.IAv1InverseTransformOutputOperator<TSample>
        {
            // The transform already applied horizontal flips while loading its columns. A vertical flip selects the completed residual row here. Each output
            // operation reads and writes exactly sixteen samples, so packed row boundaries need no padding. Prediction and destination can share storage.
            for (int row = 0; row < height; row++)
            {
                int sourceRow = flip ? height - row - 1 : row;
                Vector512<int> residual = Av1Transform2dOperations.RoundShift(residuals[sourceRow], shift);
                ref TSample predicted = ref Unsafe.Add(ref prediction, (row * predictionStride) + column);
                ref TSample target = ref Unsafe.Add(ref destination, (row * destinationStride) + column);

                TOutputOperator.Add(ref predicted, ref target, residual, bitDepth);
            }
        }

        /// <summary>
        /// Adds, clips and stores one column with the same sample rules as the SIMD output workers.
        /// </summary>
        /// <typeparam name="TSample">The native prediction and destination sample type.</typeparam>
        /// <typeparam name="TOutputOperator">The existing addition, clipping and packing operator.</typeparam>
        /// <param name="prediction">The original predicted samples.</param>
        /// <param name="predictionStride">The prediction row stride in native samples.</param>
        /// <param name="destination">The original reconstruction storage.</param>
        /// <param name="destinationStride">The destination row stride in native samples.</param>
        /// <param name="residuals">The rounded second-axis values.</param>
        /// <param name="column">The destination column.</param>
        /// <param name="height">The number of reconstructed rows.</param>
        /// <param name="flip">Whether reconstructed rows read residuals in reverse order.</param>
        /// <param name="bitDepth">The coded sample precision used by the output operator.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteColumn<TSample, TOutputOperator>(
            ReadOnlySpan<TSample> prediction,
            int predictionStride,
            Span<TSample> destination,
            int destinationStride,
            ReadOnlySpan<int> residuals,
            int column,
            int height,
            bool flip,
            int bitDepth)
            where TSample : unmanaged
            where TOutputOperator : struct, Av1InverseTransformer.IAv1InverseTransformOutputOperator<TSample>
        {
            for (int row = 0; row < height; row++)
            {
                int sourceRow = flip ? height - row - 1 : row;
                int readIndex = (row * predictionStride) + column;
                int writeIndex = (row * destinationStride) + column;

                destination[writeIndex] = TOutputOperator.Add(prediction[readIndex], residuals[sourceRow], bitDepth);
            }
        }
    }
}
