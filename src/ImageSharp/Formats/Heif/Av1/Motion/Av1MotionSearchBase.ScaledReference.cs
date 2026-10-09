// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

internal static partial class Av1MotionSearchBase
{
    /// <summary>
    /// A reference of another size than the frame, from which a fractional search measures its candidates.
    /// The full-pixel search reads the copy of the reference resized to the frame size.
    /// The fractional search predicts each candidate from the reference itself, with its scale factors and the regular filter.
    /// </summary>
    /// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
    public readonly ref struct ScaledReference<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// The bordered luma plane of the reference.
        /// </summary>
        private readonly ReadOnlySpan<TSample> samples;

        /// <summary>
        /// The row stride of the reference plane.
        /// </summary>
        private readonly int stride;

        /// <summary>
        /// The index of the first visible reference sample.
        /// </summary>
        private readonly int planeOrigin;

        /// <summary>
        /// The visible size of the reference plane.
        /// </summary>
        private readonly Size planeSize;

        /// <summary>
        /// The scale factors from the frame to the reference.
        /// </summary>
        private readonly Av1ReferenceScale scale;

        /// <summary>
        /// The luma origin of the searched block in the frame.
        /// </summary>
        private readonly Point blockOrigin;

        /// <summary>
        /// The intermediate rows of the two-dimensional convolution.
        /// </summary>
        private readonly Span<short> intermediateRows;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScaledReference{TSample}"/> struct.
        /// </summary>
        /// <param name="samples">The bordered luma plane of the reference.</param>
        /// <param name="stride">The row stride of the reference plane.</param>
        /// <param name="planeOrigin">The index of the first visible reference sample.</param>
        /// <param name="planeSize">The visible size of the reference plane.</param>
        /// <param name="scale">The scale factors from the frame to the reference.</param>
        /// <param name="blockOrigin">The luma origin of the searched block in the frame.</param>
        /// <param name="intermediateRows">The intermediate rows of the two-dimensional convolution.</param>
        public ScaledReference(
            ReadOnlySpan<TSample> samples,
            int stride,
            int planeOrigin,
            Size planeSize,
            Av1ReferenceScale scale,
            Point blockOrigin,
            Span<short> intermediateRows)
        {
            this.samples = samples;
            this.stride = stride;
            this.planeOrigin = planeOrigin;
            this.planeSize = planeSize;
            this.scale = scale;
            this.blockOrigin = blockOrigin;
            this.intermediateRows = intermediateRows;
        }

        /// <summary>
        /// Gets a value indicating whether the search measures its candidates from a scaled reference.
        /// </summary>
        public bool IsScaled => !this.samples.IsEmpty;

        /// <summary>
        /// Predicts the block at a candidate vector from the reference with its scale factors and the regular filter.
        /// The fractional search measures each candidate with this prediction.
        /// </summary>
        /// <typeparam name="TOperator">The closed prediction and error operator.</typeparam>
        /// <param name="vector">The candidate vector in eighth samples.</param>
        /// <param name="prediction">The packed prediction destination.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="bitDepth">The coded precision.</param>
        public void Predict<TOperator>(Av1MotionVector vector, Span<TSample> prediction, Size blockSize, int bitDepth)
            where TOperator : struct, IMotionSearchOperator<TSample>
            => this.Predict<TOperator>(
                vector, prediction, blockSize, Av1InterpolationFilter.Regular, Av1InterpolationFilter.Regular, bitDepth);

        /// <summary>
        /// Predicts the block at a candidate vector from the reference with its scale factors.
        /// </summary>
        /// <typeparam name="TOperator">The closed prediction and error operator.</typeparam>
        /// <param name="vector">The candidate vector in eighth samples.</param>
        /// <param name="prediction">The packed prediction destination.</param>
        /// <param name="blockSize">The block size.</param>
        /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
        /// <param name="verticalFilter">The vertical interpolation filter.</param>
        /// <param name="bitDepth">The coded precision.</param>
        public void Predict<TOperator>(
            Av1MotionVector vector,
            Span<TSample> prediction,
            Size blockSize,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            int bitDepth)
            where TOperator : struct, IMotionSearchOperator<TSample>
        {
            // The block origin and the eighth-sample vector both convert to sixteenth-sample units before the scale maps them into the reference.
            // The integer part of the scaled position selects the first reference sample, and the fraction is the first filter phase.
            Point position = this.scale.ScalePosition(
                (this.blockOrigin.X << 4) + (vector.Column << 1),
                (this.blockOrigin.Y << 4) + (vector.Row << 1),
                this.planeSize.Width,
                this.planeSize.Height,
                0,
                0);

            TOperator.PredictScaled(
                this.samples,
                this.stride,
                this.planeOrigin + ((position.Y >> Av1ReferenceScale.SubpixelBits) * this.stride) + (position.X >> Av1ReferenceScale.SubpixelBits),
                prediction,
                blockSize.Width,
                blockSize.Height,
                horizontalFilter,
                verticalFilter,
                position.X & Av1ReferenceScale.SubpixelMask,
                this.scale.HorizontalStep,
                position.Y & Av1ReferenceScale.SubpixelMask,
                this.scale.VerticalStep,
                this.intermediateRows,
                bitDepth);
        }
    }
}
