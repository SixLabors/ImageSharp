// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Keeps the screen content decision of intra frames for the inter frames that follow. For a key frame of a lookahead sequence, it decides again
/// by coding the frame without and with the screen content tools.
/// </content>
internal static partial class Av1FrameEncoder
{
    /// <summary>
    /// Holds the screen content decision of the last intra frame of a sequence. Only intra frames set the decision. The inter frames that follow keep it.
    /// </summary>
    internal struct ScreenContentDecision
    {
        /// <summary>
        /// Gets or sets a value indicating whether the frames allow the screen content tools.
        /// </summary>
        public bool AllowScreenContentTools { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the frames are classified as screen content.
        /// </summary>
        public bool IsScreenContent { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the content detection allows intra block copy.
        /// </summary>
        public bool AllowIntraBlockCopy { get; set; }
    }

    internal abstract partial class SequenceEncoder
    {
        /// <summary>
        /// The lowest quantizer index at which the screen content trial codes a lossy frame.
        /// </summary>
        private const int ScreenContentTrialQIndex = 244;

        private readonly Motion.Av1IntegerMotionVectorDecision integerMotionVectorDecision = new();

        private ScreenContentDecision screenContent;

        /// <summary>
        /// Gets the screen content decision of the last intra frame.
        /// </summary>
        private protected ref ScreenContentDecision ScreenContent => ref this.screenContent;

        /// <summary>
        /// Gets or sets the base quantizer index that the encoder set last. The temporal dependency model and every coded frame set it. A key frame
        /// reads it to set up its default coefficient models before the screen content trial chooses its own quantizer.
        /// </summary>
        private protected int CommonBaseQIndex { get; set; }

        /// <summary>
        /// Gets a value indicating whether the last coded frame allowed the screen content tools when it set its speed features, before any screen
        /// content trial. The temporal filter reads this value until the next frame sets its speed features, also when the temporal dependency
        /// model filters a trial group.
        /// </summary>
        private protected bool SpeedFeatureScreenContentTools
            => this.PictureBuffer.Picture.Parent.ScreenContentToolsBeforeTrial ?? this.FrameHeader.AllowScreenContentTools;

        /// <summary>
        /// Codes a key frame twice when the content detection left the screen content tools off. Both passes use a fixed 32x32 partition at a high
        /// quantizer. The first pass codes without the tools and the second pass codes with them. If the tools code the frame much better, the method
        /// turns them on. After the trial, the frame is ready for coding at its own quantizer.
        /// </summary>
        /// <typeparam name="TSample">The native sample storage type.</typeparam>
        /// <typeparam name="TOperator">The block encoding operations for the sample type.</typeparam>
        /// <param name="source">The coded source frame.</param>
        /// <param name="reconstruction">The frame the trial reconstructs into, which the frame coding overwrites.</param>
        /// <param name="qIndex">The base quantizer index of the frame.</param>
        /// <param name="lossless">Whether the rate control requests lossless coding.</param>
        /// <param name="isScreenContent">Whether the frame is classified as screen content.</param>
        /// <returns>Whether the frame is classified as screen content after the trial.</returns>
        private protected bool DetermineScreenContentWithEncoding<TSample, TOperator>(
            Av1EncoderFrame<TSample> source,
            Av1EncoderFrame<TSample> reconstruction,
            int qIndex,
            bool lossless,
            bool isScreenContent)
            where TSample : unmanaged
            where TOperator : struct, Av1IntraSuperblockEncoder.IBlockEncodingOperator<TSample>
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            this.PictureBuffer.Picture.Parent.ScreenContentToolsBeforeTrial = null;
            this.PictureBuffer.Picture.Parent.ScreenContentTrialQIndex = -1;

            // Only key frames get the trial. Speed 6 and higher skip it. A frame that already allows the tools does not need it.
            if (this.Options.Speed >= HeifEncodingSpeed.Level6 ||
                frameHeader.FrameType != ObuFrameType.KeyFrame ||
                frameHeader.AllowScreenContentTools)
            {
                return isScreenContent;
            }

            Av1PictureParentControlSet parent = this.PictureBuffer.Picture.Parent;
            bool allowIntraBlockCopy = frameHeader.AllowIntraBlockCopy;
            int trialQIndex = lossless ? qIndex : Math.Max(qIndex, ScreenContentTrialQIndex);

            // The default coefficient models come from the quantizer that was set before the trial, not from the trial quantizer.
            int modelQIndex = this.CommonBaseQIndex;
            parent.FixedPartitionSize = Av1BlockSize.Block32x32;
            parent.ScreenContentToolsBeforeTrial = false;
            parent.ScreenContentTrialQIndex = trialQIndex;
            frameHeader.AllowIntraBlockCopy = false;
            Span<double> psnr = stackalloc double[2];
            int palettePixelCount = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                frameHeader.AllowScreenContentTools = pass == 1;
                this.PictureBuffer.Reset(frameHeader);
                this.ApplyLaggedQuantizer(trialQIndex);
                parent.RetainsFrameProbabilities = pass == 1;
                this.SymbolEncoder.BeginFrame(null, trialQIndex, modelQIndex);
                Av1TileEncoder.AnalyzeIntraFrame<TSample, TOperator>(
                    this.SymbolEncoder,
                    source,
                    reconstruction,
                    this.PictureBuffer.Picture,
                    this.Coefficients,
                    this.TileWorkspace,
                    this.BlockWorkspace);

                psnr[pass] = GetFramePsnr<TSample, TOperator>(source, reconstruction);
                palettePixelCount = parent.PalettePixelCount;
            }

            parent.FixedPartitionSize = Av1BlockSize.Invalid;
            parent.RetainsFrameProbabilities = true;
            this.CommonBaseQIndex = trialQIndex;

            // The tools code the frame much better when the PSNR gain is more than 0.9 dB, or when the gain is large relative to the palette ratio,
            // that is the part of the frame that palette blocks cover. A ratio of 0.01% or more needs a gain per ratio of more than 4. A ratio of 5% or
            // more with a gain of more than 0.1 dB needs a gain per ratio of more than 2.
            double psnrDifference = psnr[1] - psnr[0];
            double paletteRatio = palettePixelCount / (double)(source.Height * source.Width);
            bool psnrDifferenceIsLarge = psnrDifference > 0.9;
            bool ratioIsLarge = paletteRatio >= 0.0001 && psnrDifference / paletteRatio > 4;
            bool ratioIsLarge2 = psnrDifference > 0.1 && paletteRatio >= 0.05 && psnrDifference / paletteRatio > 2;
            bool muchBetter = psnrDifferenceIsLarge || ratioIsLarge || ratioIsLarge2;

            // The trial turns intra block copy off. The frame gets back the intra block copy decision that it had before the trial.
            frameHeader.AllowScreenContentTools = muchBetter;
            parent.ScreenContentToolsBeforeTrial = muchBetter ? false : null;
            frameHeader.AllowIntraBlockCopy = allowIntraBlockCopy;
            if (muchBetter)
            {
                isScreenContent = true;
            }

            this.screenContent.AllowScreenContentTools = frameHeader.AllowScreenContentTools;
            this.screenContent.IsScreenContent = isScreenContent;
            this.PictureBuffer.Reset(frameHeader);
            this.ApplyLaggedQuantizer(qIndex);
            return isScreenContent;
        }

        /// <summary>
        /// Decides whether an inter frame with the screen content tools codes integer motion vectors only. A sequence header value other than
        /// "select" sets the decision directly. The estimated mode search of speed 7 and higher never uses integer motion vectors only. A frame
        /// without the source shown before it does not use them either.
        /// </summary>
        /// <typeparam name="TSample">The native sample storage type.</typeparam>
        /// <typeparam name="TOperator">The block comparison operations for the sample type.</typeparam>
        /// <param name="source">The source of the frame.</param>
        /// <param name="lastSource">The source shown before the frame, or <see langword="null"/>.</param>
        private protected void DecideIntegerMotionVectors<TSample, TOperator>(
            Av1EncoderFrame<TSample> source,
            Av1EncoderFrame<TSample>? lastSource)
            where TSample : unmanaged
            where TOperator : struct, Motion.Av1IntraBlockCopySearchIndex.ISearchOperation<TSample>
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            frameHeader.ForceIntegerMotionVector = false;
            bool estimatedModeSearch = !this.Options.IsAllIntra && this.Options.Speed >= HeifEncodingSpeed.Level7;
            if (frameHeader.IsIntra || !frameHeader.AllowScreenContentTools || estimatedModeSearch)
            {
                return;
            }

            if (this.SequenceHeader.ForceIntegerMotionVector != Av1Constants.SelectIntegerMotionVector)
            {
                frameHeader.ForceIntegerMotionVector = this.SequenceHeader.ForceIntegerMotionVector != 0;
                return;
            }

            if (lastSource is { } last)
            {
                frameHeader.ForceIntegerMotionVector = this.integerMotionVectorDecision.Decide<TSample, TOperator>(
                    source.CodedView.GetPlane(Av1Plane.Y), last.CodedView.GetPlane(Av1Plane.Y));
            }

            // Integer motion vectors have no fractional part, so high precision motion vectors are off.
            frameHeader.AllowHighPrecisionMotionVector &= !frameHeader.ForceIntegerMotionVector;
        }

        /// <summary>
        /// Returns the PSNR of the visible planes of a reconstruction over all their samples together, at the stream bit depth.
        /// </summary>
        /// <typeparam name="TSample">The native sample storage type.</typeparam>
        /// <typeparam name="TOperator">The block encoding operations for the sample type.</typeparam>
        /// <param name="source">The coded source frame.</param>
        /// <param name="reconstruction">The reconstruction of the frame.</param>
        /// <returns>The PSNR in decibels, at most 100.</returns>
        private static double GetFramePsnr<TSample, TOperator>(Av1EncoderFrame<TSample> source, Av1EncoderFrame<TSample> reconstruction)
            where TSample : unmanaged
            where TOperator : struct, Av1IntraSuperblockEncoder.IBlockEncodingOperator<TSample>
        {
            // A monochrome frame counts two neutral chroma planes of 4:2:0 size. Their error is zero, but their samples add to the sample count.
            int subsamplingX = source.IsMonochrome ? 1 : source.ChromaSubsamplingX;
            int subsamplingY = source.IsMonochrome ? 1 : source.ChromaSubsamplingY;
            int chromaWidth = (source.Width + subsamplingX) >> subsamplingX;
            int chromaHeight = (source.Height + subsamplingY) >> subsamplingY;
            long squaredError = GetPlaneSquaredError<TSample, TOperator>(source, reconstruction, Av1Plane.Y, source.Width, source.Height);
            if (!source.IsMonochrome)
            {
                squaredError += GetPlaneSquaredError<TSample, TOperator>(source, reconstruction, Av1Plane.U, chromaWidth, chromaHeight);
                squaredError += GetPlaneSquaredError<TSample, TOperator>(source, reconstruction, Av1Plane.V, chromaWidth, chromaHeight);
            }

            double samples = (uint)((source.Width * source.Height) + (2 * chromaWidth * chromaHeight));
            double peak = (1 << source.LumaBitDepth) - 1;
            if (squaredError <= 0)
            {
                return 100;
            }

            double value = 10.0 * Math.Log10(samples * peak * peak / squaredError);
            return value > 100 ? 100 : value;
        }

        /// <summary>
        /// Returns the squared error of the visible samples of one plane.
        /// </summary>
        /// <typeparam name="TSample">The native sample storage type.</typeparam>
        /// <typeparam name="TOperator">The block encoding operations for the sample type.</typeparam>
        /// <param name="source">The coded source frame.</param>
        /// <param name="reconstruction">The reconstruction of the frame.</param>
        /// <param name="plane">The plane to measure.</param>
        /// <param name="width">The visible width of the plane in samples.</param>
        /// <param name="height">The visible height of the plane in samples.</param>
        /// <returns>The sum of the squared sample differences.</returns>
        private static long GetPlaneSquaredError<TSample, TOperator>(
            Av1EncoderFrame<TSample> source,
            Av1EncoderFrame<TSample> reconstruction,
            Av1Plane plane,
            int width,
            int height)
            where TSample : unmanaged
            where TOperator : struct, Av1IntraSuperblockEncoder.IBlockEncodingOperator<TSample>
        {
            Av1PlaneRegion<TSample> sourcePlane = source.View.GetPlane(plane);
            Av1PlaneRegion<TSample> reconstructionPlane = reconstruction.View.GetPlane(plane);
            TOperator.GetMoments(
                Av1TransformBlockEncoder.GetPlaneSpan(sourcePlane, Point.Empty),
                sourcePlane.Stride,
                Av1TransformBlockEncoder.GetPlaneSpan(reconstructionPlane, Point.Empty),
                reconstructionPlane.Stride,
                width,
                height,
                out _,
                out long squares);

            return squares;
        }
    }
}
