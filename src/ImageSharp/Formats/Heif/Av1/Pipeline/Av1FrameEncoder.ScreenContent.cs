// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Keeps the screen content decision of intra frames for the inter frames that follow, and decides it again for a
/// key frame of a lookahead sequence by coding the frame without and with the screen content tools.
/// </content>
internal static partial class Av1FrameEncoder
{
    /// <summary>
    /// Holds the screen content decision of the last intra frame of a sequence, which the inter frames keep.
    /// Reference: cm->features.allow_screen_content_tools and cpi->is_screen_content_type, which
    /// av1_set_screen_content_options() and av1_determine_sc_tools_with_encoding() set for intra frames only.
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
    }

    internal abstract partial class SequenceEncoder
    {
        /// <summary>
        /// The quantizer the trial codes a lossy frame at, at least. Reference: the AOMMAX(q_orig, 244) of
        /// av1_determine_sc_tools_with_encoding().
        /// </summary>
        private const int ScreenContentTrialQIndex = 244;

        private readonly Motion.Av1IntegerMotionVectorDecision integerMotionVectorDecision = new();

        private ScreenContentDecision screenContent;

        /// <summary>
        /// Gets the screen content decision of the last intra frame.
        /// </summary>
        private protected ref ScreenContentDecision ScreenContent => ref this.screenContent;

        /// <summary>
        /// Gets or sets the base quantizer index that the encoder last set, which a key frame reads when it sets up
        /// its default coefficient models before the screen content trial chooses its own quantizer. The temporal
        /// dependency model and every coded frame set it. Reference: cm->quant_params.base_qindex, as
        /// init_mc_flow_dispenser() and av1_set_quantizer() leave it.
        /// </summary>
        private protected int CommonBaseQIndex { get; set; }

        /// <summary>
        /// Codes a key frame whose content detection left the screen content tools off twice, on a fixed 32x32
        /// partition at a high quantizer, without and then with the tools, and turns the tools on when they code it
        /// much better. The frame is then ready to be coded at its own quantizer. Reference:
        /// av1_determine_sc_tools_with_encoding() with set_encoding_params_for_screen_content() and
        /// screen_content_tools_determination().
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

            // Speed 6 and above skip the trial, as does a frame that already allows the tools. Reference:
            // disable_extra_sc_testing, and the use_screen_content_tools and KEY_FRAME tests of
            // av1_determine_sc_tools_with_encoding().
            if (this.Options.Speed >= HeifEncodingSpeed.Level6 ||
                frameHeader.FrameType != ObuFrameType.KeyFrame ||
                frameHeader.AllowScreenContentTools)
            {
                return isScreenContent;
            }

            Av1PictureParentControlSet parent = this.PictureBuffer.Picture.Parent;
            bool allowIntraBlockCopy = frameHeader.AllowIntraBlockCopy;
            int trialQIndex = lossless ? qIndex : Math.Max(qIndex, ScreenContentTrialQIndex);

            // The default coefficient models come from the quantizer set before the trial. Reference: the
            // av1_setup_frame() call that precedes the two passes.
            int modelQIndex = this.CommonBaseQIndex;
            parent.FixedPartitionSize = Av1BlockSize.Block32x32;
            parent.ScreenContentToolsBeforeTrial = false;
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

            // Reference: screen_content_tools_determination().
            double psnrDifference = psnr[1] - psnr[0];
            double paletteRatio = palettePixelCount / (double)(source.Height * source.Width);
            bool psnrDifferenceIsLarge = psnrDifference > 0.9;
            bool ratioIsLarge = paletteRatio >= 0.0001 && psnrDifference / paletteRatio > 4;
            bool ratioIsLarge2 = psnrDifference > 0.1 && paletteRatio >= 0.05 && psnrDifference / paletteRatio > 2;
            bool muchBetter = psnrDifferenceIsLarge || ratioIsLarge || ratioIsLarge2;

            // The intra block copy decision stays, as no trial block could use it.
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
        /// Decides whether an inter frame with the screen content tools codes integer motion vectors only. The
        /// estimated mode search of real-time usage never does, and a frame without the source shown before it
        /// does not. Reference: the cur_frame_force_integer_mv setup of encode_frame_to_data_rate().
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
            bool estimatedModeSearch = !this.SequenceHeader.IsStillPicture && this.Options.Speed >= HeifEncodingSpeed.Level7;
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

            // Reference: av1_set_high_precision_mv() with cur_frame_force_integer_mv.
            frameHeader.AllowHighPrecisionMotionVector &= !frameHeader.ForceIntegerMotionVector;
        }

        /// <summary>
        /// Returns the PSNR of the visible planes of a reconstruction over all their samples together. Reference:
        /// psnr[0] of aom_calc_highbd_psnr() at the stream bit depth.
        /// </summary>
        private static double GetFramePsnr<TSample, TOperator>(Av1EncoderFrame<TSample> source, Av1EncoderFrame<TSample> reconstruction)
            where TSample : unmanaged
            where TOperator : struct, Av1IntraSuperblockEncoder.IBlockEncodingOperator<TSample>
        {
            // A monochrome frame keeps two neutral chroma planes of 4:2:0 size in the reference, whose error is zero.
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
        /// Returns the squared error of the visible samples of one plane. Reference: get_sse() and highbd_get_sse().
        /// </summary>
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
