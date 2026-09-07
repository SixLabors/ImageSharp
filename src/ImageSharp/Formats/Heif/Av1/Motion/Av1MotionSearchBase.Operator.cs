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
        /// Builds the final inter predictor and its residual for transform-based winner selection.
        /// </summary>
        /// <param name="source">The source samples at the block origin.</param>
        /// <param name="sourceStride">The source row stride.</param>
        /// <param name="reference">The complete bordered reference plane.</param>
        /// <param name="referenceStride">The reference row stride.</param>
        /// <param name="referenceOrigin">The displaced integer reference origin.</param>
        /// <param name="prediction">The packed prediction destination.</param>
        /// <param name="residual">The packed residual destination.</param>
        /// <param name="scratch">The signed intermediate convolution storage.</param>
        /// <param name="width">The prediction width.</param>
        /// <param name="height">The prediction height.</param>
        /// <param name="horizontalFilter">The final horizontal interpolation family.</param>
        /// <param name="verticalFilter">The final vertical interpolation family.</param>
        /// <param name="horizontalPhase">The horizontal phase in one-sixteenth-sample units.</param>
        /// <param name="verticalPhase">The vertical phase in one-sixteenth-sample units.</param>
        /// <param name="bitDepth">The coded sample precision.</param>
        public static abstract void PreparePrediction(
            ReadOnlySpan<TSample> source,
            int sourceStride,
            ReadOnlySpan<TSample> reference,
            int referenceStride,
            int referenceOrigin,
            Span<TSample> prediction,
            Span<short> residual,
            Span<short> scratch,
            int width,
            int height,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int horizontalPhase,
            int verticalPhase,
            int bitDepth);

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
    }
}
