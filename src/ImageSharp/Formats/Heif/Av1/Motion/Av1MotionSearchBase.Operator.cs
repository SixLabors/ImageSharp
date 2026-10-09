// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

internal static partial class Av1MotionSearchBase
{
    /// <summary>
    /// Measures unsigned sample planes without changing the motion controller's error domains.
    /// </summary>
    /// <typeparam name="TSample">The unsigned component storage type.</typeparam>
    public interface IMotionSearchOperator<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// Builds the final rounded inter predictor without materializing a source residual.
        /// </summary>
        /// <param name="reference">The complete bordered reference plane.</param>
        /// <param name="referenceStride">The reference row stride.</param>
        /// <param name="referenceOrigin">The displaced integer origin.</param>
        /// <param name="prediction">The prediction destination.</param>
        /// <param name="predictionStride">The prediction row stride. The prediction width selects a packed prediction.</param>
        /// <param name="intermediateRows">The signed intermediate convolution storage.</param>
        /// <param name="width">The prediction width.</param>
        /// <param name="height">The prediction height.</param>
        /// <param name="horizontalFilter">The horizontal interpolation family.</param>
        /// <param name="verticalFilter">The vertical interpolation family.</param>
        /// <param name="horizontalPhase">The horizontal phase in sixteenth-sample units.</param>
        /// <param name="verticalPhase">The vertical phase in sixteenth-sample units.</param>
        /// <param name="bitDepth">The coded component precision.</param>
        public static abstract void BuildPrediction(
            ReadOnlySpan<TSample> reference,
            int referenceStride,
            int referenceOrigin,
            Span<TSample> prediction,
            int predictionStride,
            Span<short> intermediateRows,
            int width,
            int height,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int verticalPhase,
            int bitDepth);

        /// <summary>
        /// Subtracts a prediction from its source into a residual packed at the block width.
        /// </summary>
        /// <param name="source">The source samples at the block origin.</param>
        /// <param name="sourceStride">The source row stride.</param>
        /// <param name="prediction">The prediction samples at the block origin.</param>
        /// <param name="predictionStride">The prediction row stride. The block width selects a packed prediction.</param>
        /// <param name="residual">The residual destination, packed at the block width.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        public static abstract void SubtractPrediction(
            ReadOnlySpan<TSample> source,
            int sourceStride,
            ReadOnlySpan<TSample> prediction,
            int predictionStride,
            Span<short> residual,
            int width,
            int height);

        /// <summary>
        /// Produces a packed fractional prediction with each filter pass rounded to sample precision.
        /// </summary>
        /// <param name="reference">The bordered reference plane.</param>
        /// <param name="referenceStride">The reference row stride.</param>
        /// <param name="referenceOrigin">The integer prediction origin.</param>
        /// <param name="buffer">The borrowed prediction and intermediate buffer.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        /// <param name="horizontalPhase">The horizontal eighth-sample phase.</param>
        /// <param name="verticalPhase">The vertical eighth-sample phase.</param>
        /// <param name="taps">The search filter's tap count.</param>
        /// <param name="bitDepth">The coded precision.</param>
        static abstract void Predict(
            ReadOnlySpan<TSample> reference,
            int referenceStride,
            int referenceOrigin,
            Span<TSample> buffer,
            int width,
            int height,
            int horizontalPhase,
            int verticalPhase,
            int taps,
            int bitDepth);

        /// <summary>
        /// Produces a prediction from a reference of another size than the frame.
        /// The source position and phase step by the scale of each axis.
        /// </summary>
        /// <param name="reference">The bordered reference plane.</param>
        /// <param name="referenceStride">The reference row stride.</param>
        /// <param name="referenceOrigin">The integer reference position of the first output sample.</param>
        /// <param name="buffer">The prediction destination.</param>
        /// <param name="bufferStride">The prediction row stride. The block width selects a packed prediction.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
        /// <param name="verticalFilter">The vertical interpolation filter.</param>
        /// <param name="horizontalPhase">The horizontal phase of the first output sample in 1/1024 samples.</param>
        /// <param name="horizontalStep">The horizontal step per output sample in 1/1024 samples.</param>
        /// <param name="verticalPhase">The vertical phase of the first output sample in 1/1024 samples.</param>
        /// <param name="verticalStep">The vertical step per output sample in 1/1024 samples.</param>
        /// <param name="intermediateRows">The intermediate rows of the two-dimensional convolution.</param>
        /// <param name="bitDepth">The coded precision.</param>
        static abstract void PredictScaled(
            ReadOnlySpan<TSample> reference,
            int referenceStride,
            int referenceOrigin,
            Span<TSample> buffer,
            int bufferStride,
            int width,
            int height,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int horizontalStep,
            int verticalPhase,
            int verticalStep,
            Span<short> intermediateRows,
            int bitDepth);

        /// <summary>
        /// Measures raw absolute differences, doubling alternate-row results before precision normalization.
        /// </summary>
        /// <param name="source">The source block samples.</param>
        /// <param name="sourceStride">The source row stride.</param>
        /// <param name="prediction">The prediction block samples.</param>
        /// <param name="predictionStride">The prediction row stride.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        /// <param name="rowStep">One for all rows or two for alternate rows.</param>
        /// <returns>The raw absolute-difference sum.</returns>
        static abstract int SumAbsoluteDifferences(
            ReadOnlySpan<TSample> source,
            int sourceStride,
            ReadOnlySpan<TSample> prediction,
            int predictionStride,
            int width,
            int height,
            int rowStep);

        /// <summary>
        /// Measures raw signed and squared residual sums without materializing a residual plane.
        /// </summary>
        /// <param name="source">The source block samples.</param>
        /// <param name="sourceStride">The source row stride.</param>
        /// <param name="prediction">The prediction block samples.</param>
        /// <param name="predictionStride">The prediction row stride.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        /// <param name="sum">The raw signed residual sum.</param>
        /// <param name="squares">The raw squared residual sum.</param>
        static abstract void GetMoments(
            ReadOnlySpan<TSample> source,
            int sourceStride,
            ReadOnlySpan<TSample> prediction,
            int predictionStride,
            int width,
            int height,
            out int sum,
            out long squares);

        /// <summary>
        /// Measures absolute differences after blending a reference with a fixed second predictor.
        /// </summary>
        /// <param name="source">The source block samples.</param>
        /// <param name="sourceStride">The source row stride.</param>
        /// <param name="prediction">The searched reference block samples.</param>
        /// <param name="predictionStride">The searched reference row stride.</param>
        /// <param name="secondPrediction">The fixed second predictor, packed at the block width.</param>
        /// <param name="mask">The six-bit weights of the searched reference, packed at the block width. An empty mask selects equal weights.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        /// <param name="rowStep">One for all rows or two for alternate rows.</param>
        /// <returns>The raw absolute-difference sum.</returns>
        static abstract int SumCompoundAbsoluteDifferences(
            ReadOnlySpan<TSample> source,
            int sourceStride,
            ReadOnlySpan<TSample> prediction,
            int predictionStride,
            ReadOnlySpan<TSample> secondPrediction,
            ReadOnlySpan<byte> mask,
            int width,
            int height,
            int rowStep);

        /// <summary>
        /// Measures source-minus-blend moments with six-bit mask weights rounded to sample precision.
        /// </summary>
        /// <param name="source">The source block samples.</param>
        /// <param name="sourceStride">The source row stride.</param>
        /// <param name="prediction">The searched reference block samples.</param>
        /// <param name="predictionStride">The searched reference row stride.</param>
        /// <param name="secondPrediction">The fixed second predictor, packed at the block width.</param>
        /// <param name="mask">The six-bit weights of the searched reference, packed at the block width. An empty mask selects equal weights.</param>
        /// <param name="width">The block width.</param>
        /// <param name="height">The block height.</param>
        /// <param name="sum">The raw signed residual sum.</param>
        /// <param name="squares">The raw squared residual sum.</param>
        static abstract void GetCompoundMoments(
            ReadOnlySpan<TSample> source,
            int sourceStride,
            ReadOnlySpan<TSample> prediction,
            int predictionStride,
            ReadOnlySpan<TSample> secondPrediction,
            ReadOnlySpan<byte> mask,
            int width,
            int height,
            out int sum,
            out long squares);
    }
}
