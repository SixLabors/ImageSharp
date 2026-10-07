// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction.Inter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

internal static partial class Av1IntraSuperblockEncoder
{
    internal partial struct ModeDecision<TSample, TOperator>
    {
        /// <summary>
        /// Returns the available references whose frame has another size than the current frame. An intra frame has
        /// none.
        /// </summary>
        /// <param name="references">The frame of each reference type.</param>
        /// <param name="parent">The frame state with the available references and the frame size.</param>
        /// <returns>One bit per scaled reference type.</returns>
        private static int GetScaledReferenceMask(ReadOnlySpan<Av1EncoderFrame<TSample>> references, Av1PictureParentControlSet parent)
        {
            ObuFrameHeader frameHeader = parent.FrameHeader;
            if (frameHeader.IsIntra || references.IsEmpty)
            {
                return 0;
            }

            int width = frameHeader.FrameSize.FrameWidth;
            int height = frameHeader.FrameSize.FrameHeight;
            int mask = 0;
            for (int reference = (int)Av1ReferenceFrameType.Last; reference <= (int)Av1ReferenceFrameType.Alternate; reference++)
            {
                Av1EncoderFrame<TSample> frame = references[reference];
                if ((parent.AvailableReferenceMask & (1 << reference)) != 0 && (frame.Width != width || frame.Height != height))
                {
                    mask |= 1 << reference;
                }
            }

            return mask;
        }

        /// <summary>
        /// Gets a value indicating whether a reference has another size than the current frame.
        /// </summary>
        /// <param name="referenceFrame">The reference type.</param>
        /// <returns><see langword="true"/> when the reference has another size.</returns>
        private readonly bool IsResizedReference(Av1ReferenceFrameType referenceFrame)
            => referenceFrame > Av1ReferenceFrameType.Intra && (this.scaledReferenceMask & (1 << (int)referenceFrame)) != 0;

        /// <summary>
        /// Gets a value indicating whether the prediction from a reference scales: the reference has another size
        /// than the current frame, and the estimated real-time search is not predicting from the resized copy.
        /// </summary>
        /// <param name="referenceFrame">The reference type.</param>
        /// <returns><see langword="true"/> when the prediction from the reference scales.</returns>
        private readonly bool IsScaledReference(Av1ReferenceFrameType referenceFrame)
            => !this.predictsFromSearchReferences && this.IsResizedReference(referenceFrame);

        /// <summary>
        /// Returns the reference from which a rate-distortion motion search predicts its fractional candidates: the
        /// luma plane of a reference whose prediction scales, or the default value for a reference of the frame size.
        /// Reference: the buffers that av1_single_motion_search() swaps back before the subpel search.
        /// </summary>
        /// <param name="referenceFrame">The reference type.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="intermediateRows">The intermediate rows of the scaled convolution.</param>
        /// <returns>The scaled search reference.</returns>
        private readonly Av1MotionSearchBase.ScaledReference<TSample> GetScaledSearchReference(
            Av1ReferenceFrameType referenceFrame,
            Point blockOrigin,
            Span<short> intermediateRows)
        {
            if (!this.IsScaledReference(referenceFrame))
            {
                return default;
            }

            Av1EncoderFrame<TSample> reference = this.references.Span[(int)referenceFrame];
            Av1PlaneRegion<TSample> plane = reference.CodedView.GetPlane(Av1Plane.Y);
            ObuFrameSize frameSize = this.picture.Parent.FrameHeader.FrameSize;
            return new Av1MotionSearchBase.ScaledReference<TSample>(
                plane.Samples,
                plane.Stride,
                (plane.Bounds.Y * plane.Stride) + plane.Bounds.X,
                new Size(reference.Width, reference.Height),
                new Av1ReferenceScale(reference.Width, reference.Height, frameSize.FrameWidth, frameSize.FrameHeight),
                blockOrigin,
                intermediateRows);
        }

        /// <summary>
        /// Returns the luma position at which a block reads a reference without a vector: the block position, or, for
        /// a reference whose prediction scales, the block position scaled into the reference.
        /// Reference: scaled_buffer_offset() in setup_pred_plane(), with which av1_setup_pred_block() points the
        /// prediction buffers of a block.
        /// </summary>
        /// <param name="referenceFrame">The reference type.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <returns>The luma position in the reference.</returns>
        private readonly Point GetReferenceBlockOrigin(Av1ReferenceFrameType referenceFrame, Point blockOrigin)
        {
            if (!this.IsScaledReference(referenceFrame))
            {
                return blockOrigin;
            }

            Av1EncoderFrame<TSample> reference = this.references.Span[(int)referenceFrame];
            ObuFrameSize frameSize = this.picture.Parent.FrameHeader.FrameSize;
            Av1ReferenceScale scale = new(reference.Width, reference.Height, frameSize.FrameWidth, frameSize.FrameHeight);

            // The scale functions take sixteenth samples, but the offset passes whole samples and drops the extra
            // precision of the result. Reference: the SCALE_EXTRA_BITS shift of scaled_buffer_offset().
            const int extraBits = Av1ReferenceScale.SubpixelBits - 4;
            return new Point(scale.ScaleHorizontal(blockOrigin.X) >> extraBits, scale.ScaleVertical(blockOrigin.Y) >> extraBits);
        }

        /// <summary>
        /// Predicts one plane rectangle from a reference of another size than the current frame. The block position
        /// plus the vector maps into the reference through its scale factors, clamps to the reference border, and
        /// the convolution then steps through the reference by the scale of each axis. Reference:
        /// init_subpel_params() with av1_scaled_x() and av1_scaled_y(), then the scaled branch of
        /// av1_make_inter_predictor().
        /// </summary>
        /// <param name="referenceFrame">The scaled reference type.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="planeOrigin">The rectangle origin in samples of the current frame plane.</param>
        /// <param name="subsamplingX">The horizontal subsampling of the plane.</param>
        /// <param name="subsamplingY">The vertical subsampling of the plane.</param>
        /// <param name="vector">The motion vector in eighth luma samples.</param>
        /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
        /// <param name="verticalFilter">The vertical interpolation filter.</param>
        /// <param name="prediction">The prediction destination, starting at the rectangle.</param>
        /// <param name="predictionStride">The destination stride.</param>
        /// <param name="width">The rectangle width.</param>
        /// <param name="height">The rectangle height.</param>
        /// <param name="predictionScratch">The intermediate storage of the two-dimensional convolution.</param>
        private readonly void PredictScaledInter(
            Av1ReferenceFrameType referenceFrame,
            Av1Plane plane,
            Point planeOrigin,
            int subsamplingX,
            int subsamplingY,
            Av1MotionVector vector,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            Span<TSample> prediction,
            int predictionStride,
            int width,
            int height,
            Span<short> predictionScratch)
        {
            Av1EncoderFrame<TSample> reference = this.references.Span[(int)referenceFrame];
            ObuFrameSize frameSize = this.picture.Parent.FrameHeader.FrameSize;
            Av1ReferenceScale scale = new(reference.Width, reference.Height, frameSize.FrameWidth, frameSize.FrameHeight);

            // The position is in sixteenth samples of the current plane, and scales to 1/1024 samples of the
            // reference plane.
            Point position = scale.ScalePosition(
                (planeOrigin.X << 4) + (vector.Column << (1 - subsamplingX)),
                (planeOrigin.Y << 4) + (vector.Row << (1 - subsamplingY)),
                Av1Math.DivideLog2Ceiling(reference.Width, subsamplingX),
                Av1Math.DivideLog2Ceiling(reference.Height, subsamplingY),
                subsamplingX,
                subsamplingY);

            TOperator.PredictScaledInter(
                reference.CodedView.GetPlane(plane),
                new Point(position.X >> Av1ReferenceScale.SubpixelBits, position.Y >> Av1ReferenceScale.SubpixelBits),
                horizontalFilter,
                verticalFilter,
                position.X & Av1ReferenceScale.SubpixelMask,
                scale.HorizontalStep,
                position.Y & Av1ReferenceScale.SubpixelMask,
                scale.VerticalStep,
                prediction,
                predictionStride,
                width,
                height,
                predictionScratch,
                this.bitDepth);
        }

        /// <summary>
        /// Predicts one plane block from a reference of another size than the current frame into a packed
        /// destination, and subtracts it from the source.
        /// </summary>
        /// <param name="referenceFrame">The scaled reference type.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="planeOrigin">The block origin in plane samples.</param>
        /// <param name="subsamplingX">The horizontal subsampling of the plane.</param>
        /// <param name="subsamplingY">The vertical subsampling of the plane.</param>
        /// <param name="vector">The motion vector in eighth luma samples.</param>
        /// <param name="horizontalFilter">The horizontal interpolation filter.</param>
        /// <param name="verticalFilter">The vertical interpolation filter.</param>
        /// <param name="predictionSize">The plane block size.</param>
        /// <param name="prediction">The packed prediction destination.</param>
        /// <param name="residual">The packed source-minus-prediction destination.</param>
        /// <param name="predictionScratch">The intermediate storage of the two-dimensional convolution.</param>
        private readonly void PrepareScaledInterPrediction(
            Av1ReferenceFrameType referenceFrame,
            Av1Plane plane,
            Point planeOrigin,
            int subsamplingX,
            int subsamplingY,
            Av1MotionVector vector,
            Av1InterpolationFilter horizontalFilter,
            Av1InterpolationFilter verticalFilter,
            Av1BlockSize predictionSize,
            Span<TSample> prediction,
            Span<short> residual,
            Span<short> predictionScratch)
        {
            int width = predictionSize.GetWidth();
            int height = predictionSize.GetHeight();
            int sampleCount = width * height;
            this.PredictScaledInter(
                referenceFrame,
                plane,
                planeOrigin,
                subsamplingX,
                subsamplingY,
                vector,
                horizontalFilter,
                verticalFilter,
                prediction,
                width,
                width,
                height,
                predictionScratch);

            Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(plane);
            TOperator.SubtractPrediction(
                Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, planeOrigin),
                sourcePlane.Stride,
                prediction[..sampleCount],
                residual[..sampleCount],
                width,
                height);
        }
    }
}
