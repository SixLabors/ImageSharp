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
        /// Returns the available references whose frame has another size than the current frame. An intra frame has none.
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
        /// Gets a value indicating whether the prediction from a reference scales. This is true when the reference has another size than
        /// the current frame and the estimated real-time search does not predict from the resized copy.
        /// </summary>
        /// <param name="referenceFrame">The reference type.</param>
        /// <returns><see langword="true"/> when the prediction from the reference scales.</returns>
        private readonly bool IsScaledReference(Av1ReferenceFrameType referenceFrame)
            => !this.predictsFromSearchReferences && this.IsResizedReference(referenceFrame);

        /// <summary>
        /// Returns the reference from which a rate-distortion motion search predicts its fractional candidates. For a reference whose
        /// prediction scales, this is its luma plane. For a reference of the frame size, this is the default value. The full-sample
        /// search reads the resized copy, but the fractional search predicts from the original reference.
        /// </summary>
        /// <param name="referenceFrame">The reference type.</param>
        /// <param name="blockOrigin">The luma block origin.</param>
        /// <param name="filterRows">The intermediate rows of the scaled convolution.</param>
        /// <returns>The scaled search reference.</returns>
        private readonly Av1MotionSearchBase.ScaledReference<TSample> GetScaledSearchReference(
            Av1ReferenceFrameType referenceFrame,
            Point blockOrigin,
            Span<short> filterRows)
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
                filterRows);
        }

        /// <summary>
        /// Returns the luma position at which a block reads a reference without a vector. For a reference whose prediction scales, this is
        /// the block position scaled into the reference. For other references, this is the block position.
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

            // The scale functions expect sixteenth samples, but this offset passes whole samples. The shift drops the extra precision of
            // the result, so the position stays in whole samples.
            const int extraBits = Av1ReferenceScale.SubpixelBits - 4;
            return new Point(scale.ScaleHorizontal(blockOrigin.X) >> extraBits, scale.ScaleVertical(blockOrigin.Y) >> extraBits);
        }

        /// <summary>
        /// Predicts one plane rectangle from a reference of another size than the current frame. The block position plus the vector maps
        /// into the reference through its scale factors and clamps to the reference border. Then the convolution steps through the
        /// reference by the scale of each axis.
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
        /// <param name="filterRows">The intermediate storage of the two-dimensional convolution.</param>
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
            Span<short> filterRows)
        {
            Av1EncoderFrame<TSample> reference = this.references.Span[(int)referenceFrame];
            ObuFrameSize frameSize = this.picture.Parent.FrameHeader.FrameSize;
            Av1ReferenceScale scale = new(reference.Width, reference.Height, frameSize.FrameWidth, frameSize.FrameHeight);

            // The position is in sixteenth samples of the current plane. A luma vector in eighth samples becomes sixteenth samples of a
            // plane without subsampling. The position scales to 1/1024 samples of the reference plane.
            Point position = scale.ScalePosition(
                (planeOrigin.X << 4) + (vector.Column << (1 - subsamplingX)),
                (planeOrigin.Y << 4) + (vector.Row << (1 - subsamplingY)),
                Av1Math.DivideLog2Ceiling(reference.Width, subsamplingX),
                Av1Math.DivideLog2Ceiling(reference.Height, subsamplingY),
                subsamplingX,
                subsamplingY);

            // The integer part of the position selects the first reference sample. The plane bounds move it into the complete padded plane.
            Av1PlaneRegion<TSample> referencePlane = reference.CodedView.GetPlane(plane);
            Rectangle referenceBounds = referencePlane.Bounds;
            int referenceOrigin =
                ((referenceBounds.Y + (position.Y >> Av1ReferenceScale.SubpixelBits)) * referencePlane.Stride) +
                referenceBounds.X +
                (position.X >> Av1ReferenceScale.SubpixelBits);

            TOperator.PredictScaled(
                referencePlane.Samples,
                referencePlane.Stride,
                referenceOrigin,
                prediction,
                predictionStride,
                width,
                height,
                horizontalFilter,
                verticalFilter,
                position.X & Av1ReferenceScale.SubpixelMask,
                scale.HorizontalStep,
                position.Y & Av1ReferenceScale.SubpixelMask,
                scale.VerticalStep,
                filterRows,
                this.bitDepth.GetBitCount());
        }

        /// <summary>
        /// Predicts one plane block from a reference of another size than the current frame into a packed
        /// destination, and subtracts it from the source.
        /// </summary>
        /// <param name="sourceLuma">The samples of the complete source luma plane, read once per frame pass.</param>
        /// <param name="sourceBlue">The samples of the complete source blue-difference plane, read once per frame pass.</param>
        /// <param name="sourceRed">The samples of the complete source red-difference plane, read once per frame pass.</param>
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
        /// <param name="filterRows">The intermediate storage of the two-dimensional convolution.</param>
        private readonly void PrepareScaledInterPrediction(
            ReadOnlySpan<TSample> sourceLuma,
            ReadOnlySpan<TSample> sourceBlue,
            ReadOnlySpan<TSample> sourceRed,
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
            Span<short> filterRows)
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
                filterRows);

            Av1PlaneRegion<TSample> sourcePlane = this.source.GetPlane(plane);
            TOperator.SubtractPrediction(
                Av1TransformBlockEncoder.GetPlaneSpan(SelectPlane(plane, sourceLuma, sourceBlue, sourceRed), sourcePlane, planeOrigin),
                sourcePlane.Stride,
                prediction[..sampleCount],
                width,
                residual[..sampleCount],
                width,
                height);
        }
    }
}
