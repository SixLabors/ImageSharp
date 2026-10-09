// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Cdef;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.LoopFilter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.TemporalFilter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Components;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Codes a good-quality sequence through the lookahead: the frames of each golden group leave display order, the
/// alternate references are coded hidden and shown later, and each temporal unit ends with a shown frame.
/// </content>
internal static partial class Av1FrameEncoder
{
    /// <summary>
    /// The time-stamp ticks per second. Reference: TICKS_PER_SEC.
    /// </summary>
    private const long TicksPerSecond = 10000000;

    /// <summary>
    /// The size of an empty temporal delimiter: its OBU header and a zero size. libaom writes it outside the frame,
    /// so the rate control does not count it. Reference: the obu_header_size of encoder_encode().
    /// </summary>
    private const int TemporalDelimiterBytes = 2;

    internal abstract partial class SequenceEncoder
    {
        /// <summary>
        /// Whether each update type found a global motion model in the current golden group, or
        /// <see cref="int.MaxValue"/> before its first frame. Reference: ppi->valid_gm_model_found.
        /// </summary>
        private readonly int[] validGlobalMotionFound = new int[(int)Av1FrameUpdateType.Count];

        /// <summary>
        /// The motion vector statistics of the last coded frame. Reference: ppi->mv_stats.
        /// </summary>
        private readonly Av1MotionVectorStatistics motionVectorStatistics = new();

        /// <summary>
        /// Whether the current frame skips the global motion search. Reference: disable_gm_search_based_on_stats().
        /// </summary>
        private bool globalMotionDisabledByStatistics;

        /// <summary>
        /// The stream a frame is packed into to measure its size before the recode decision, created on first use.
        /// Reference: the dummy pack of encode_with_recode_loop() into the output buffer.
        /// </summary>
        private MemoryStream? measuredFrameStream;

        /// <summary>
        /// Gets the constant-quality index of the sequence. Reference: cq_level.
        /// </summary>
        private protected int ConstantQualityIndex => this.constantQualityIndex;

        /// <summary>
        /// Gets the number of times the lookahead coded a frame again because its size missed the bit target.
        /// </summary>
        internal int RecodedFrameCount { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the encoder replaces residuals outside the visible frame. Good-quality
        /// usage does so with the default objective delta-q mode and the temporal dependency model on, without adaptive
        /// quantization, unless the sharpness is 3. Reference: the do_border_pad test of av1_encode().
        /// </summary>
        private protected bool UsesBorderPad =>
            !this.Options.IsAllIntra &&
            this.Options.Speed < HeifEncodingSpeed.Level7 &&
            this.Options.DeltaQMode == Av1DeltaQMode.Objective &&
            this.Options.EnableTemporalModel &&
            this.Options.AdaptiveQuantizationMode == Av1AdaptiveQuantizationMode.None &&
            this.Options.Sharpness != 3;

        /// <summary>
        /// Encodes the frames of a sequence with the lookahead of <see cref="Av1EncoderOptions.LagInFrames"/> and
        /// records where each sample ends. Reference: the encoder_encode() loop of the good-quality usage with a lag,
        /// which pushes one frame, runs the lookahead stage, and codes frames until one is shown.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <param name="image">The image that holds the frames.</param>
        /// <param name="firstFrameIndex">The index of the first frame to encode.</param>
        /// <param name="frameCount">The number of frames to encode.</param>
        /// <param name="frameDurationTicks">The frame duration in time-stamp ticks. Reference: ts_duration.</param>
        /// <param name="stream">The destination stream.</param>
        /// <param name="sampleEnds">Receives the stream length after each sample, one per frame.</param>
        /// <param name="syncSamples">Receives whether each sample holds a shown key frame.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public abstract void EncodeWithLookahead<TPixel>(
            Image<TPixel> image,
            int firstFrameIndex,
            int frameCount,
            long frameDurationTicks,
            Stream stream,
            Span<long> sampleEnds,
            Span<bool> syncSamples,
            CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>;

        /// <summary>
        /// Returns whether every reference of the current frame precedes it in display order while the sequence
        /// codes with a lookahead. Reference: refs_are_one_sided() and the lag_in_frames test of
        /// check_skip_mode_enabled().
        /// </summary>
        /// <returns><see langword="true"/> when skip mode is off for one-sided references.</returns>
        private protected bool HasOnlyPastReferencesWithLag()
        {
            if (this.Options.LagInFrames <= 0 || this.FrameHeader.IsIntra)
            {
                return false;
            }

            ReadOnlySpan<uint> referenceFrameIndices = this.FrameHeader.GetReferenceFrameIndices();
            for (int index = 0; index < Av1Constants.ReferencesPerFrame; index++)
            {
                if (this.BlockWorkspace.ReferenceFrameNumbers[(int)referenceFrameIndices[index]] > this.BlockWorkspace.EncodedFrameCount)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Creates the frame-level decisions of the lookahead for this sequence.
        /// </summary>
        /// <param name="frameDurationTicks">The frame duration in time-stamp ticks.</param>
        /// <returns>The decisions.</returns>
        private protected Av1SecondPass CreateSecondPass(long frameDurationTicks)
        {
            ObuFrameSize frameSize = this.FrameHeader.FrameSize;
            return new Av1SecondPass(
                frameSize.FrameWidth,
                frameSize.FrameHeight,
                this.SequenceHeader.ColorConfig.BitDepth,
                this.constantQualityIndex,
                Av1QuantizationLookup.GetQIndex(this.Options.MinimumQuantizer),
                Av1QuantizationLookup.GetQIndex(this.Options.MaximumQuantizer),
                (int)this.Options.Speed,
                this.Options.LagInFrames,
                (double)TicksPerSecond / frameDurationTicks,
                this.Options.KeyFrameMaximumDistance,
                this.Options.Sharpness,
                this.Options.RateControlMode,
                null);
        }

        /// <summary>
        /// Points each reference type at the frame in its slot and returns the context of the primary reference.
        /// </summary>
        /// <param name="parent">The frame state that receives the available references.</param>
        /// <returns>The primary reference context, or <see langword="null"/> for the default distributions.</returns>
        private protected abstract Av1FrameEntropyContext? BindReferences(Av1PictureParentControlSet parent);

        /// <summary>
        /// Analyzes a lagged frame and, under a bit budget, packs each coding to measure its size and codes the frame
        /// again at the quantizer the rate control picks while the size misses its target. A discarded coding moves
        /// the frame probabilities as libaom's does, and the next coding starts from a clean picture. The frame before a
        /// forced key frame records its unfiltered error. The frame is then ready for its loop filters and final pack.
        /// Reference: encode_with_recode_loop() and the ambient_err of encode_with_recode_loop_and_filter().
        /// </summary>
        /// <typeparam name="TSample">The native sample storage type.</typeparam>
        /// <typeparam name="TOperator">The block encoding operations for the sample type.</typeparam>
        /// <typeparam name="TTextureOperator">The motion vector statistics texture operations.</typeparam>
        /// <typeparam name="TGlobalMotionOperator">The global motion search operations.</typeparam>
        /// <param name="secondPass">The lookahead decisions and rate control.</param>
        /// <param name="frame">The decisions of the frame.</param>
        /// <param name="parent">The frame state.</param>
        /// <param name="source">The coded source frame.</param>
        /// <param name="references">The retained frames indexed by prediction reference identifier.</param>
        /// <param name="searchReferences">The frames the motion search reads, indexed by prediction reference identifier.</param>
        /// <param name="pool">The reference pool of the sequence.</param>
        /// <param name="current">The buffer the frame is coded into.</param>
        /// <param name="writeSequenceHeader">Whether a sequence header OBU precedes the frame.</param>
        /// <param name="qIndex">The quantizer index of the first coding.</param>
        /// <param name="switchableBeforeFix">Receives whether the frame filter of the kept coding was switchable before the filter fix.</param>
        /// <returns>The quantizer index of the kept coding.</returns>
        private protected int AnalyzeLaggedFrame<TSample, TOperator, TTextureOperator, TGlobalMotionOperator>(
            Av1SecondPass secondPass,
            in Av1SecondPassFrame frame,
            Av1PictureParentControlSet parent,
            Av1EncoderFrame<TSample> source,
            Av1EncoderFrame<TSample>[] references,
            Av1EncoderFrame<TSample>[] searchReferences,
            Av1EncoderReferencePool<TSample> pool,
            Av1EncoderReferencePool<TSample>.Entry current,
            bool writeSequenceHeader,
            int qIndex,
            out bool switchableBeforeFix)
            where TSample : unmanaged
            where TOperator : struct, Av1IntraSuperblockEncoder.IBlockEncodingOperator<TSample>
            where TTextureOperator : struct, Av1MotionVectorStatistics.ITextureOperator<TSample>
            where TGlobalMotionOperator : struct, IGlobalMotionSearchOperator<TSample>
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            Av1PictureControlSet picture = this.PictureBuffer.Picture;
            Size frameSize = new(source.Width, source.Height);
            int superblockTargetRate = Av1RateControl.GetSuperblockTargetRate(secondPass.FrameTarget, source.Width, source.Height);
            switchableBeforeFix = Av1TileEncoder.AnalyzeFrame<TSample, TOperator>(
                this.SymbolEncoder, source, references, searchReferences, current.Buffer.Frame, picture, this.Coefficients, this.TileWorkspace, this.BlockWorkspace);

            while (secondPass.UsesRecodeLoop)
            {
                // Each measuring pack finalizes the frame, which steps the film grain seed. Reference: the
                // av1_finalize_encoded_frame() call of the dummy pack.
                this.PrepareFilmGrain();
                Av1TileEncoder.PackFrame<TSample, TOperator>(
                    this.SymbolEncoder, source, references, searchReferences, current.Buffer.Frame, picture, this.Coefficients, this.TileWorkspace, this.BlockWorkspace);

                long packedBits = this.MeasureLaggedFrame(Av1TileEncoder.FromPackedTiles(picture, this.SymbolEncoder), writeSequenceHeader);

                // av1_collect_mv_stats() gathers the statistics of each coding, which the next coding reads.
                this.CompleteLaggedMotionVectorStatistics<TSample, TTextureOperator>(parent, source);
                long keyFrameError = secondPass.MatchesAmbientError
                    ? GetLumaSquaredError<TSample, TOperator>(source, current.Buffer.Frame)
                    : 0;

                int nextQIndex = qIndex;
                if (!secondPass.UpdateRecodeQuantizer((int)Math.Min(packedBits, int.MaxValue), keyFrameError, ref nextQIndex))
                {
                    break;
                }

                // The discarded coding moved the frame probabilities at the end of its encode_frame_internal().
                Av1TileEncoder.UpdateFrameProbabilities(picture, this.BlockWorkspace, switchableBeforeFix);
                this.UpdateMotionModeProbabilities(parent);
                this.RecodedFrameCount++;

                qIndex = nextQIndex;
                bool highPrecision = frameHeader.AllowHighPrecisionMotionVector;
                this.BeginLaggedRecode(parent, in frame, qIndex, frameSize);
                this.SymbolEncoder.BeginFrame(this.BindReferences(parent), frameHeader.QuantizationParameters.BaseQIndex);

                // A change of vector precision searches global motion again. Reference: the last_loop_allow_hp test.
                if (frameHeader.AllowHighPrecisionMotionVector != highPrecision)
                {
                    this.SearchGlobalMotion<TSample, TGlobalMotionOperator>(source, references, parent);
                }

                this.BeginSegmentation(
                    pool,
                    current,
                    parent,
                    allowsRecode: true,
                    frame.MacroblockAverageEnergy,
                    secondPass.BestQuality,
                    secondPass.WorstQuality,
                    superblockTargetRate);

                switchableBeforeFix = Av1TileEncoder.AnalyzeFrame<TSample, TOperator>(
                    this.SymbolEncoder, source, references, searchReferences, current.Buffer.Frame, picture, this.Coefficients, this.TileWorkspace, this.BlockWorkspace);
            }

            // The frame before a forced key frame records its unfiltered error. Only a bit budget reads it.
            if (secondPass.UsesRecodeLoop && secondPass.RecordsAmbientError)
            {
                secondPass.SetAmbientError(GetLumaSquaredError<TSample, TOperator>(source, current.Buffer.Frame));
            }

            return qIndex;
        }

        /// <summary>
        /// Returns the size in bits of the frame OBUs of a packed frame, with the sequence header of a shown key frame
        /// and without a temporal delimiter. Reference: the size of the dummy av1_pack_bitstream() of
        /// encode_with_recode_loop().
        /// </summary>
        /// <param name="tiles">The packed tiles of the frame.</param>
        /// <param name="writeSequenceHeader">Whether a sequence header OBU precedes the frame.</param>
        /// <returns>The frame size in bits.</returns>
        private protected long MeasureLaggedFrame(Av1TileEncoder tiles, bool writeSequenceHeader)
        {
            MemoryStream stream = this.measuredFrameStream ??= new MemoryStream();
            stream.SetLength(0);

            // The frame header carries the deblocking levels of the last coded frame until the frame picks its own.
            ObuLoopFilterParameters loopFilter = this.FrameHeader.LoopFilterParameters;
            Span<int> frameLevels = stackalloc int[4];
            CopyLoopFilterLevels(loopFilter, frameLevels);
            SetLoopFilterLevels(loopFilter, this.BlockWorkspace.CodedLoopFilterLevels);

            long bits;
            if (writeSequenceHeader)
            {
                this.ObuWriter.WriteSequenceFrame(stream, this.SequenceHeader, this.FrameHeader, tiles);
                bits = (stream.Length - TemporalDelimiterBytes) * 8;
            }
            else
            {
                this.ObuWriter.WriteFrameWithoutDelimiter(stream, this.SequenceHeader, this.FrameHeader, tiles);
                bits = stream.Length * 8;
            }

            SetLoopFilterLevels(loopFilter, frameLevels);
            return bits;
        }

        /// <summary>
        /// Keeps the deblocking levels of a coded frame for the measuring packs of the next frame.
        /// </summary>
        private protected void RecordCodedLoopFilterLevels()
        {
            ObuLoopFilterParameters loopFilter = this.FrameHeader.LoopFilterParameters;
            CopyLoopFilterLevels(loopFilter, this.BlockWorkspace.CodedLoopFilterLevels);
        }

        /// <summary>
        /// Copies the luma vertical, luma horizontal, U and V deblocking levels of a frame header.
        /// </summary>
        /// <param name="loopFilter">The loop filter parameters of the frame header.</param>
        /// <param name="levels">Receives the four levels.</param>
        private static void CopyLoopFilterLevels(ObuLoopFilterParameters loopFilter, Span<int> levels)
        {
            levels[0] = loopFilter.FilterLevel[0];
            levels[1] = loopFilter.FilterLevel[1];
            levels[2] = loopFilter.FilterLevelU;
            levels[3] = loopFilter.FilterLevelV;
        }

        /// <summary>
        /// Sets the luma vertical, luma horizontal, U and V deblocking levels of a frame header.
        /// </summary>
        /// <param name="loopFilter">The loop filter parameters of the frame header.</param>
        /// <param name="levels">The four levels.</param>
        private static void SetLoopFilterLevels(ObuLoopFilterParameters loopFilter, ReadOnlySpan<int> levels)
        {
            loopFilter.FilterLevel[0] = levels[0];
            loopFilter.FilterLevel[1] = levels[1];
            loopFilter.FilterLevelU = levels[2];
            loopFilter.FilterLevelV = levels[3];
        }

        /// <summary>
        /// Writes the OBUs of a coded frame: a shown key frame after a temporal delimiter and the sequence header, the
        /// first frame of a temporal unit after a temporal delimiter, and any other frame alone. Reference:
        /// av1_pack_bitstream() with the temporal delimiter of encoder_encode().
        /// </summary>
        /// <param name="stream">The destination stream.</param>
        /// <param name="tiles">The packed tiles of the frame.</param>
        /// <param name="writeSequenceHeader">Whether a sequence header OBU precedes the frame.</param>
        /// <param name="writeTemporalDelimiter">Whether a temporal delimiter OBU precedes the frame.</param>
        private protected void WriteLaggedFrame(Stream stream, Av1TileEncoder tiles, bool writeSequenceHeader, bool writeTemporalDelimiter)
        {
            if (writeSequenceHeader)
            {
                this.ObuWriter.WriteSequenceFrame(stream, this.SequenceHeader, this.FrameHeader, tiles);
            }
            else if (writeTemporalDelimiter)
            {
                this.ObuWriter.WriteFrame(stream, this.SequenceHeader, this.FrameHeader, tiles);
            }
            else
            {
                this.ObuWriter.WriteFrameWithoutDelimiter(stream, this.SequenceHeader, this.FrameHeader, tiles);
            }
        }

        /// <summary>
        /// Prepares a frame to be coded again at a new quantizer: the picture state starts clean while the frame
        /// keeps its probabilities, motion search step and the tools a coding can only turn off, the quantizer and the
        /// speed features follow the new quantizer, and the motion vector precision is chosen again from the
        /// statistics of the last coding. Reference: the start of each pass of the encode_with_recode_loop() loop.
        /// </summary>
        /// <param name="parent">The frame state.</param>
        /// <param name="frame">The decisions of the frame.</param>
        /// <param name="qIndex">The quantizer index of the next coding.</param>
        /// <param name="frameSize">The frame dimensions.</param>
        private protected void BeginLaggedRecode(Av1PictureParentControlSet parent, in Av1SecondPassFrame frame, int qIndex, Size frameSize)
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            this.PictureBuffer.Reset(frameHeader);
            parent.RetainsFrameProbabilities = true;
            parent.RecodesFrame = true;
            this.ApplyLaggedQuantizer(qIndex);
            parent.SpeedSettings = new(
                this.Options.Speed,
                this.Options.IsAllIntra,
                frameHeader.IsIntra,
                parent.FrameUpdateType,
                qIndex,
                frameSize,
                sharpness: this.Options.Sharpness,
                tuning: this.Options.Tuning);

            // Intra block copy stays as the last coding left it, because libaom sets allow_intrabc once per frame and a
            // coding that used no copy turns it off.
            this.ConfigureRecodedReferenceTools(parent);
            this.BeginLaggedMotionVectorStatistics(in frame, parent, frameSize);
            this.CommonBaseQIndex = frameHeader.QuantizationParameters.BaseQIndex;
        }

        /// <summary>
        /// Returns the luma squared error of the visible samples of a reconstruction. Reference: aom_get_y_sse() and
        /// aom_highbd_get_y_sse().
        /// </summary>
        /// <typeparam name="TSample">The native sample storage type.</typeparam>
        /// <typeparam name="TOperator">The block encoding operations for the sample type.</typeparam>
        /// <param name="source">The coded source frame.</param>
        /// <param name="reconstruction">The reconstruction.</param>
        /// <returns>The squared error.</returns>
        private protected static long GetLumaSquaredError<TSample, TOperator>(Av1EncoderFrame<TSample> source, Av1EncoderFrame<TSample> reconstruction)
            where TSample : unmanaged
            where TOperator : struct, Av1IntraSuperblockEncoder.IBlockEncodingOperator<TSample>
            => GetPlaneSquaredError<TSample, TOperator>(source, reconstruction, Av1Plane.Y, source.Width, source.Height);

        /// <summary>
        /// Returns the bits a frame added to the stream, without the temporal delimiter that starts its temporal unit.
        /// Reference: the frame_size of av1_get_compressed_data(), in bits.
        /// </summary>
        /// <param name="stream">The destination stream.</param>
        /// <param name="start">The stream length before the frame.</param>
        /// <param name="wroteTemporalDelimiter">Whether the frame starts its temporal unit.</param>
        /// <returns>The frame size in bits.</returns>
        private protected static long GetLaggedFrameBits(Stream stream, long start, bool wroteTemporalDelimiter)
            => (stream.Length - start - (wroteTemporalDelimiter ? TemporalDelimiterBytes : 0)) * 8;

        /// <summary>
        /// Encodes the frames of a sequence through the lookahead at one sample type: frames enter the lookahead and
        /// the first pass measures them, the lookahead decisions order each golden group, the temporal filter and the
        /// temporal dependency model run at the start of each group, and each temporal unit ends with a shown frame.
        /// Reference: the encoder_encode() loop of the good-quality usage with a lag, av1_get_compressed_data() and
        /// av1_encode_strategy().
        /// </summary>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <typeparam name="TSample">The sample type.</typeparam>
        /// <typeparam name="TStorer">The sample conversion.</typeparam>
        /// <typeparam name="TFirstPassOperator">The first-pass arithmetic.</typeparam>
        /// <typeparam name="TFilterOperator">The temporal filter arithmetic.</typeparam>
        /// <typeparam name="TSearchOperator">The motion search arithmetic.</typeparam>
        /// <typeparam name="TTplOperator">The temporal dependency model arithmetic.</typeparam>
        /// <param name="coder">The frame coder of the sample type.</param>
        /// <param name="image">The image that holds the frames.</param>
        /// <param name="firstFrameIndex">The index of the first frame to encode.</param>
        /// <param name="frameCount">The number of frames to encode.</param>
        /// <param name="frameDurationTicks">The frame duration in time-stamp ticks. Reference: ts_duration.</param>
        /// <param name="stream">The destination stream.</param>
        /// <param name="sampleEnds">Receives the stream length after each sample, one per frame.</param>
        /// <param name="syncSamples">Receives whether each sample holds a shown key frame.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private protected void EncodeLagged<TPixel, TSample, TStorer, TFirstPassOperator, TFilterOperator, TSearchOperator, TTplOperator>(
            ILaggedFrameCoder<TSample> coder,
            Image<TPixel> image,
            int firstFrameIndex,
            int frameCount,
            long frameDurationTicks,
            Stream stream,
            Span<long> sampleEnds,
            Span<bool> syncSamples,
            CancellationToken cancellationToken)
            where TPixel : unmanaged, IPixel<TPixel>
            where TSample : unmanaged
            where TStorer : struct, IHeifSampleConverter<TSample>
            where TFirstPassOperator : struct, Av1FirstPassOperator.IOperator<TSample>
            where TFilterOperator : struct, Av1TemporalFilter.ITemporalFilterOperator<TSample>, Av1TemporalFilter.ISharpPredictionOperator<TSample>
            where TSearchOperator : struct, Av1MotionSearchBase.IMotionSearchOperator<TSample>
            where TTplOperator : struct, IAv1TplSampleOperator<TSample>
        {
            Av1ColorFormat colorFormat = this.SequenceHeader.ColorConfig.GetColorFormat();
            int bitDepth = this.SequenceHeader.ColorConfig.BitDepth.GetBitCount();
            int lumaBorder = (this.SequenceHeader.Use128x128Superblock ? 128 : 64) + 32;
            Av1SecondPass secondPass = this.CreateSecondPass(frameDurationTicks);
            using Av1LookaheadQueue<TSample> lookahead = new(
                this.Configuration,
                image.Width,
                image.Height,
                bitDepth,
                colorFormat,
                CenteredChromaSamplePosition,
                CenteredChromaSamplePosition,
                lumaBorder,
                secondPass.LookaheadDepth);

            using Av1FirstPass<TSample, TFirstPassOperator> firstPass = new(
                this.Configuration,
                image.Width,
                image.Height,
                this.SequenceHeader.ColorConfig.BitDepth,
                this.Options.Speed,
                this.SequenceHeader.Use128x128Superblock ? 128 : 64,
                doBorderPad: this.UsesBorderPad,
                calculateWaveletEnergy: false,
                sharpness: this.Options.Sharpness,
                tuning: this.Options.Tuning,
                lookaheadStage: true,
                tiles: this.FrameHeader.TilesInfo);

            using LookaheadTemporalFilter<TSample, TFilterOperator, TSearchOperator>? filter =
                this.Options.EnableTemporalFilter
                    ? new(this.Configuration, image.Width, image.Height, bitDepth, colorFormat, lumaBorder, this.Options, this.ConstantQualityIndex)
                    : null;

            // Before the first frame the filter and the model read the motion settings of a key frame, which no
            // quantizer-dependent update has changed yet. High precision vectors are always allowed. Reference: the
            // speed features of av1_change_config(), and the av1_set_high_precision_mv(cpi, 1, 0) call that starts
            // av1_get_compressed_data().
            Av1MotionSearchSettings keyFrameMotionSettings = new(this.Options.Speed, false, image.Size, -1, true, false);
            using LookaheadTemporalModel<TSample, TSearchOperator, TTplOperator>? temporalModel =
                this.Options.EnableTemporalModel && this.Options.LagInFrames > 1
                    ? new(this, lookahead, coder.ReferencePool, filter, image.Width, image.Height, colorFormat, secondPass.LagInFrames) { MotionSettings = keyFrameMotionSettings }
                    : null;

            if (temporalModel is not null)
            {
                secondPass.AttachGopLengthEvaluator(temporalModel);
            }

            Rectangle sourceRectangle = new(0, 0, image.Width, image.Height);
            int pushed = 0;
            int sample = 0;
            int codedFrames = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (pushed < frameCount)
                {
                    // av1_lookahead_push(): the source joins the lookahead with its border extended, and the lookahead
                    // stage measures it at once.
                    Av1EncoderFrameBuffer<TSample> buffer = lookahead.BeginPush();
                    PrepareSource<TPixel, TSample, TStorer>(
                        this.Configuration,
                        image.Frames[firstFrameIndex + pushed],
                        sourceRectangle,
                        buffer.Frame,
                        this.ConversionWorkspace);

                    buffer.Frame.ExtendBorders();
                    lookahead.EndPush();
                    Av1EncoderFrameBuffer<TSample>? previous = lookahead.Peek(lookahead.Count - 2);
                    Av1FirstPassStatistics statistics = firstPass.Process(
                        buffer.Frame,
                        (previous ?? buffer).Frame,
                        frameDurationTicks,
                        forceKeyFrame: false,
                        secondPass.Group.Size == 0 ? Av1FrameUpdateType.Key : secondPass.Group.UpdateTypes[0]);

                    secondPass.PushStatistics(statistics);
                    pushed++;
                }

                bool flush = pushed == frameCount;
                bool temporalUnitStarted = false;
                while (secondPass.TryBeginFrame(flush, out Av1SecondPassFrame frame))
                {
                    if (frame.ShowExistingFrame)
                    {
                        long start = stream.Length;
                        this.WriteShowExistingFrame(stream, in frame, !temporalUnitStarted);
                        secondPass.CompleteFrame(0, GetLaggedFrameBits(stream, start, !temporalUnitStarted));
                    }
                    else
                    {
                        // The filter reads the motion settings and frame flags the encoder holds from the previous
                        // frame.
                        Av1MotionSearchSettings motionSettings = codedFrames == 0
                            ? keyFrameMotionSettings
                            : this.PictureBuffer.Picture.Parent.MotionSearchSettings;

                        if (filter is not null && frame.GroupIndex == 0)
                        {
                            // With the model, the lookahead decisions discard the previous group's filtered frames
                            // before the group length test filters the trial group.
                            if (temporalModel is null && frame.StartsGroup)
                            {
                                filter.Reset();
                            }

                            filter.FilterGroup(
                                lookahead,
                                secondPass,
                                true,
                                this.FrameHeader.ForceIntegerMotionVector,
                                this.SpeedFeatureScreenContentTools,
                                motionSettings);
                        }

                        // An intra frame decides its screen content from the unfiltered source before its quantizer,
                        // filtering and model read the decision. Reference: av1_set_screen_content_options() before
                        // denoise_and_encode() in av1_encode_strategy().
                        Av1EncoderFrameBuffer<TSample> source = lookahead.Peek(frame.SourceOffset)!;
                        bool isScreenContent = coder.DecideLaggedScreenContent(source, frame.IsKeyFrame);
                        secondPass.SetScreenContentType(isScreenContent);

                        // denoise_and_encode() picks the quantizer and the filtered source first.
                        if (filter is not null)
                        {
                            source = filter.SelectSource(
                                lookahead,
                                secondPass,
                                ref frame,
                                source,
                                true,
                                this.FrameHeader.ForceIntegerMotionVector,
                                this.SpeedFeatureScreenContentTools,
                                motionSettings,
                                isScreenContent);
                        }

                        // It then sets the motion search step of a key frame, an alternate reference or a golden frame
                        // once before the frame sets it again for its own search.
                        if (frame.IsKeyFrame || frame.UpdateType is Av1FrameUpdateType.Alternate or Av1FrameUpdateType.Golden)
                        {
                            this.PrepareMotionSearchStep(frame.IsKeyFrame, image.Size, motionSettings);
                        }

                        // The model measures a new group after its frames are filtered. Reference: the "perform tpl after
                        // filtering" block of av1_encode_strategy().
                        temporalModel?.BeginFrame(secondPass, in frame);

                        Av1EncoderFrameBuffer<TSample>? lastSource = frame.ShowFrame && frame.SourceOffset == 0 && frame.FrameNumber > 0
                            ? lookahead.Peek(-1)
                            : null;

                        coder.EncodeLaggedFrame(source, in frame, secondPass, stream, !temporalUnitStarted, temporalModel, lastSource);
                        codedFrames++;
                    }

                    temporalUnitStarted = true;
                    if (frame.ShowFrame)
                    {
                        lookahead.Pop();
                        sampleEnds[sample] = stream.Length;
                        syncSamples[sample] = frame.IsKeyFrame;
                        sample++;
                        break;
                    }
                }

                if (flush && secondPass.PendingFrameCount == 0)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Sets the frame header fields of a frame of a lookahead golden group before its source analysis: the frame
        /// type, the show flags, and the order hint of its display position. Reference: the frame parameters that
        /// av1_encode_strategy() passes to av1_encode(), with current_frame->order_hint.
        /// </summary>
        /// <param name="frame">The decisions of the frame.</param>
        private protected void ConfigureLaggedFrameHeader(in Av1SecondPassFrame frame)
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            this.ConfigureFrameHeader(frame.IsKeyFrame ? ObuFrameType.KeyFrame : ObuFrameType.InterFrame);
            int orderHintBits = this.SequenceHeader.OrderHintInfo.OrderHintBits;
            frameHeader.OrderHint = orderHintBits == 0 ? 0 : (uint)frame.DisplayOrder & ((1U << orderHintBits) - 1);
            frameHeader.ShowFrame = frame.ShowFrame;
            frameHeader.ShowableFrame = frame.ShowableFrame;
        }

        /// <summary>
        /// Selects the reference slots, the refreshed slots, and the primary reference of a frame of a lookahead
        /// golden group. Reference: the reference setup of av1_encode_strategy(), and copy_frame_prob_info() at a key
        /// frame.
        /// </summary>
        /// <param name="parent">The frame state.</param>
        /// <param name="frame">The decisions of the frame.</param>
        private protected void ConfigureLaggedReferenceStructure(Av1PictureParentControlSet parent, in Av1SecondPassFrame frame)
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            Av1GoodQualityReferenceStructure.GroupFrame groupFrame = new(
                frame.UpdateType,
                frame.LayerDepth,
                frame.MaxLayerDepth,
                frame.DisplayOrder,
                frame.ResetsReferences,
                isNonReference: false,
                showExisting: false);

            this.goodQualityStructure.ConfigureLagged(frameHeader, in groupFrame, []);
            parent.FrameUpdateType = frame.UpdateType;
            parent.StartsGoldenGroup = frame.UpdateType == Av1FrameUpdateType.Golden;

            // av1_configure_buffer_updates(): the golden refresh of the update role.
            parent.RefreshesGolden = frame.UpdateType is Av1FrameUpdateType.Key or Av1FrameUpdateType.Golden or
                Av1FrameUpdateType.Overlay || (frame.UpdateType == Av1FrameUpdateType.Alternate && frame.ResetsReferences);

            // av1_configure_buffer_updates(): the alternate reference refresh of the update role.
            parent.RefreshesAlternate = frame.UpdateType is Av1FrameUpdateType.Key or Av1FrameUpdateType.Alternate ||
                (frame.UpdateType == Av1FrameUpdateType.Overlay && frame.ResetsReferences);

            if (frameHeader.FrameType == ObuFrameType.KeyFrame)
            {
                this.warpedProbabilities.AsSpan().Fill(64);
                DefaultObmcProbabilities.CopyTo(this.obmcProbabilities, 0);
            }
        }

        /// <summary>
        /// Sets the motion search step of a key frame, an alternate reference or a golden frame once before the frame
        /// sets it again for its own search. Only the vector magnitude it leaves behind lasts: a key frame seeds it with
        /// the frame range, and an inter frame discards the previous frame's maximum, so the frame's own search starts
        /// from the default step. Reference: the av1_set_mv_search_params() call of denoise_and_encode().
        /// </summary>
        /// <param name="isKeyFrame">Whether the frame is a key frame.</param>
        /// <param name="frameSize">The frame dimensions.</param>
        /// <param name="motionSettings">The motion search settings the encoder holds.</param>
        private protected void PrepareMotionSearchStep(bool isKeyFrame, Size frameSize, Av1MotionSearchSettings motionSettings)
        {
            if (motionSettings.AutomaticStepSizeLevel == 0)
            {
                return;
            }

            this.PictureBuffer.Picture.Parent.MaximumMotionVectorMagnitude = isKeyFrame
                ? Math.Max(frameSize.Width, frameSize.Height)
                : -1;
        }

        /// <summary>
        /// Decides whether the frame searches global motion from the models its golden group found so far. A group
        /// with an alternate reference stops searching once its alternate, intermediate alternate and leaf frames
        /// have all been coded without a model. Reference: the valid_gm_model_found reset of
        /// av1_compute_global_motion_facade() and disable_gm_search_based_on_stats().
        /// </summary>
        /// <param name="frame">The decisions of the frame.</param>
        /// <param name="group">The golden group of the frame.</param>
        private protected void BeginLaggedGlobalMotion(in Av1SecondPassFrame frame, Av1GopStructure group)
        {
            if (frame.GroupIndex == 0)
            {
                this.validGlobalMotionFound.AsSpan().Fill(int.MaxValue);
            }

            this.globalMotionDisabledByStatistics = group.ArfIndex > -1 &&
                this.validGlobalMotionFound[(int)Av1FrameUpdateType.Alternate] == 0 &&
                this.validGlobalMotionFound[(int)Av1FrameUpdateType.IntermediateAlternate] == 0 &&
                this.validGlobalMotionFound[(int)Av1FrameUpdateType.Last] == 0;
        }

        /// <summary>
        /// Chooses the motion vector precision of an inter frame from its quantizer and the statistics of the last
        /// coded frame, then discards those statistics and, at the speeds that read them, lets the packing pass
        /// collect the frame's own. Reference: the av1_pick_and_set_high_precision_mv() call of
        /// encode_with_recode_loop() and the mv_stats reset and collection after av1_encode_frame().
        /// </summary>
        /// <param name="frame">The decisions of the frame.</param>
        /// <param name="parent">The frame state.</param>
        /// <param name="frameSize">The frame dimensions.</param>
        private protected void BeginLaggedMotionVectorStatistics(in Av1SecondPassFrame frame, Av1PictureParentControlSet parent, Size frameSize)
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            bool readsLastFrameData = parent.SpeedSettings.UsesLastMotionVectorData;
            bool allowsSmartPrecision = !frameHeader.IsIntra &&
                frame.UpdateType is not (Av1FrameUpdateType.Overlay or Av1FrameUpdateType.IntermediateOverlay);

            if (!frameHeader.IsIntra)
            {
                bool highPrecision = this.motionVectorStatistics.PickHighPrecision(
                    this.QIndex,
                    readsLastFrameData,
                    allowsSmartPrecision,
                    (int)frameHeader.OrderHint,
                    frameSize);

                frameHeader.AllowHighPrecisionMotionVector = highPrecision && !frameHeader.ForceIntegerMotionVector;
            }

            this.motionVectorStatistics.DiscardIfValid();
            bool collects = readsLastFrameData && allowsSmartPrecision;
            parent.MotionVectorStatistics = collects ? this.motionVectorStatistics : null;
            if (collects)
            {
                this.motionVectorStatistics.BeginFrame(frameHeader.AllowHighPrecisionMotionVector);
            }
        }

        /// <summary>
        /// Completes the motion vector statistics the packing pass collected from a frame. Reference: the texture sums
        /// of collect_mv_stats_b() and the end of av1_collect_mv_stats().
        /// </summary>
        /// <typeparam name="TSample">The sample type.</typeparam>
        /// <typeparam name="TOperator">The texture operator of the sample type.</typeparam>
        /// <param name="parent">The frame state.</param>
        /// <param name="source">The frame source.</param>
        private protected void CompleteLaggedMotionVectorStatistics<TSample, TOperator>(Av1PictureParentControlSet parent, Av1EncoderFrame<TSample> source)
            where TSample : unmanaged
            where TOperator : struct, Av1MotionVectorStatistics.ITextureOperator<TSample>
        {
            if (parent.MotionVectorStatistics is null)
            {
                return;
            }

            parent.MotionVectorStatistics.CompleteFrame<TSample, TOperator>(
                source.View.GetPlane(Av1Plane.Y),
                source.LumaBitDepth,
                this.QIndex,
                (int)this.FrameHeader.OrderHint);

            parent.MotionVectorStatistics = null;
        }

        /// <summary>
        /// Records whether the coded frame found a global motion model for its update type. Reference: update_gm_stats().
        /// </summary>
        /// <param name="updateType">The update type of the frame.</param>
        private protected void CompleteLaggedGlobalMotion(Av1FrameUpdateType updateType)
        {
            int present = 0;
            foreach (Av1GlobalMotionParameters model in this.FrameHeader.GetGlobalMotionParameters())
            {
                if (model.Type != Av1GlobalMotionType.Identity)
                {
                    present = 1;
                    break;
                }
            }

            ref int found = ref this.validGlobalMotionFound[(int)updateType];
            found = found == int.MaxValue ? present : found | present;
            this.globalMotionDisabledByStatistics = false;
        }

        /// <summary>
        /// Applies the quantizer that the lookahead chose for the frame. Reference: av1_set_quantizer() with the q of
        /// av1_rc_pick_q_and_bounds().
        /// </summary>
        /// <param name="qIndex">The base quantizer index.</param>
        private protected void ApplyLaggedQuantizer(int qIndex)
        {
            this.QIndex = qIndex;
            ApplyFrameQuantizer(this.FrameHeader, this.SequenceHeader, qIndex, this.Options);

            // The delta quantizer of each tile's first superblock is measured from the frame quantizer, which the
            // picture reset read before this frame chose it. Reference: the current_base_qindex set at the start of
            // each tile.
            this.PictureBuffer.Picture.Parent.PreviousQIndex.Span.Fill(qIndex);
        }

        /// <summary>
        /// Writes a frame that shows the coded frame of a display position, and leaves the references unchanged.
        /// Reference: encode_show_existing_frame() and the OBU_FRAME_HEADER of av1_pack_bitstream().
        /// </summary>
        /// <param name="stream">The destination stream.</param>
        /// <param name="frame">The decisions of the frame.</param>
        /// <param name="writeTemporalDelimiter">Whether the frame starts a temporal unit.</param>
        private protected void WriteShowExistingFrame(Stream stream, in Av1SecondPassFrame frame, bool writeTemporalDelimiter)
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            int slot = this.goodQualityStructure.GetExistingFrameSlot(frame.DisplayOrder);
            if (slot < 0)
            {
                throw new InvalidOperationException("The frame to show is not in a reference slot.");
            }

            frameHeader.ShowExistingFrame = true;
            frameHeader.FrameToShowMapIdx = (uint)slot;
            this.ObuWriter.WriteShowExistingFrame(stream, this.SequenceHeader, frameHeader, writeTemporalDelimiter);
            frameHeader.ShowExistingFrame = false;
        }
    }

    private sealed partial class ByteSequenceEncoder : SequenceEncoder.ILaggedFrameCoder<byte>
    {
        /// <inheritdoc/>
        public Av1EncoderReferencePool<byte> ReferencePool => this.referencePool;

        /// <inheritdoc/>
        public override void EncodeWithLookahead<TPixel>(
            Image<TPixel> image,
            int firstFrameIndex,
            int frameCount,
            long frameDurationTicks,
            Stream stream,
            Span<long> sampleEnds,
            Span<bool> syncSamples,
            CancellationToken cancellationToken)
            => this.EncodeLagged<TPixel, byte, HeifByteSampleConverter, Av1FirstPassOperator.ByteOperator, Av1TemporalFilter.ByteOperator, Av1MotionSearchBase.ByteOperator, Av1TplByteOperator>(
                this, image, firstFrameIndex, frameCount, frameDurationTicks, stream, sampleEnds, syncSamples, cancellationToken);

        /// <inheritdoc/>
        public bool DecideLaggedScreenContent(Av1EncoderFrameBuffer<byte> unfilteredSource, bool isKeyFrame)
        {
            if (isKeyFrame)
            {
                DecideScreenContent(unfilteredSource.Frame, this.SequenceHeader, this.Options, ref this.ScreenContent);
            }

            return this.ScreenContent.IsScreenContent;
        }

        /// <inheritdoc/>
        public void EncodeLaggedFrame(
            Av1EncoderFrameBuffer<byte> source,
            in Av1SecondPassFrame frame,
            Av1SecondPass secondPass,
            Stream stream,
            bool writeTemporalDelimiter,
            ILaggedTemporalModel<byte>? temporalModel,
            Av1EncoderFrameBuffer<byte>? lastSource)
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            this.ConfigureLaggedFrameHeader(in frame);

            Av1EncoderReferencePool<byte>.Entry current = this.referencePool.Acquire();
            ApplyScreenContentTools(
                this.SequenceHeader, frameHeader, this.Options, new Size(source.Frame.Width, source.Frame.Height), this.ScreenContent);

            bool isScreenContent = this.ScreenContent.IsScreenContent;

            this.DecideIntegerMotionVectors<byte, Av1IntraSuperblockEncoder.ByteOperator>(source.Frame, lastSource?.Frame);

            this.PictureBuffer.Reset(frameHeader);
            Av1PictureParentControlSet parent = this.PictureBuffer.Picture.Parent;
            parent.RecodesFrame = false;
            parent.PreviousSource = default;
            parent.SourceBlockSad = default;
            parent.HighSourceSad = false;
            parent.FrameSourceSad = 0;
            parent.SourceMotionPercentage = 0;

            // The rate control keeps the key and golden counters. Reference: rc->frames_since_key and
            // rc->frames_since_golden.
            parent.FramesSinceKey = secondPass.FramesSinceKey;
            parent.FramesSinceGolden = secondPass.FramesSinceGolden;
            parent.IsScreenContent = isScreenContent;
            parent.IsGraphicsAnimation = frame.IsGraphicsAnimation;

            this.ConfigureLaggedReferenceStructure(parent, in frame);

            // Reference distances follow display order, and a hidden frame keeps the count of the frames shown
            // before it. Reference: current_frame->display_order_hint and current_frame->frame_number.
            this.BlockWorkspace.EncodedFrameCount = frame.DisplayOrder;
            this.BlockWorkspace.FrameNumber = frame.DisplayOrder - frame.SourceOffset;

            // The model statistics of the frame feed its quantizer choice. Reference: process_tpl_stats_frame() before
            // av1_rc_pick_q_and_bounds() in set_size_dependent_vars().
            double qStepRatio = 1;
            bool tplFrameValid = false;
            bool tplReady = temporalModel?.ApplyToFrame(parent, frame.GroupIndex, out qStepRatio, out tplFrameValid) ?? false;
            if (temporalModel is null)
            {
                parent.TplFrame = null;
                parent.TplStatisticsReady = false;
            }

            int qIndex = secondPass.ChooseBaseQIndex(isScreenContent, tplReady, tplFrameValid, parent.TplImportance, qStepRatio);
            this.ApplyLaggedQuantizer(qIndex);

            // The lookahead makes this a statistics-consuming stage, so inter frames scale the rate multiplier by
            // their layer depth and the golden boost. Reference: is_stat_consumption_stage() in av1_compute_rd_mult().
            parent.IsStatConsumptionStage = true;
            parent.LayerDepth = frame.LayerDepth;
            parent.GoldenBoost = secondPass.GoldenBoost;

            parent.EncoderOptions = this.Options;
            parent.EncoderBorder = this.GetEncoderBorder();
            parent.ConstantQualityIndex = GetConstantQualityLevel(this.Options, this.ConstantQualityIndex);
            parent.SpeedSettings = new(
                this.Options.Speed,
                this.Options.IsAllIntra,
                frameHeader.IsIntra,
                parent.FrameUpdateType,
                this.QIndex,
                new Size(source.Frame.Width, source.Frame.Height),
                sharpness: this.Options.Sharpness,
                tuning: this.Options.Tuning);

            parent.BorderPad = this.UsesBorderPad;
            this.BeginLaggedMotionVectorStatistics(in frame, parent, new Size(source.Frame.Width, source.Frame.Height));

            this.ConfigureReferenceTools(parent);
            this.ResetIntraSegmentation();

            // Reference: the av1_determine_sc_tools_with_encoding() call of encode_with_recode_loop(), which a
            // lookahead sequence reaches because its statistics allow recoding.
            isScreenContent = this.DetermineScreenContentWithEncoding<byte, Av1IntraSuperblockEncoder.ByteOperator>(
                source.Frame,
                current.Buffer.Frame,
                qIndex,
                secondPass.BestQuality == 0 && secondPass.WorstQuality == 0,
                isScreenContent);

            parent.IsScreenContent = isScreenContent;
            this.CommonBaseQIndex = frameHeader.QuantizationParameters.BaseQIndex;
            this.SymbolEncoder.BeginFrame(this.BindReferences(parent), frameHeader.QuantizationParameters.BaseQIndex);
            this.BeginLaggedGlobalMotion(in frame, secondPass.Group);
            this.SearchGlobalMotion<byte, ByteGlobalMotionSearchOperator>(source.Frame, this.references, parent);
            this.BeginSegmentation(
                this.referencePool,
                current,
                parent,
                allowsRecode: true,
                frame.MacroblockAverageEnergy,
                secondPass.BestQuality,
                secondPass.WorstQuality,
                Av1RateControl.GetSuperblockTargetRate(secondPass.FrameTarget, source.Frame.Width, source.Frame.Height));

            bool writeSequenceHeader = frameHeader.FrameType == ObuFrameType.KeyFrame && frameHeader.ShowFrame;
            Av1PictureControlSet picture = this.PictureBuffer.Picture;
            qIndex = this.AnalyzeLaggedFrame<byte, Av1IntraSuperblockEncoder.ByteOperator, Av1MotionVectorStatistics.ByteTextureOperator, ByteGlobalMotionSearchOperator>(
                secondPass,
                in frame,
                parent,
                source.Frame,
                this.references,
                this.searchReferences,
                this.referencePool,
                current,
                writeSequenceHeader,
                qIndex,
                out bool switchableBeforeFix);

            this.PrepareFilmGrain();
            Av1TileEncoder.CompleteFrame<byte, Av1IntraSuperblockEncoder.ByteOperator,
                Av1DeblockingFilter.VerticalByteEdgeOperator, Av1DeblockingFilter.HorizontalByteEdgeOperator, Av1CdefEncoder.ByteOperator>(
                this.SymbolEncoder,
                source.Frame,
                this.references,
                this.searchReferences,
                current.Buffer.Frame,
                picture,
                this.Coefficients,
                this.TileWorkspace,
                this.BlockWorkspace,
                switchableBeforeFix);

            long start = stream.Length;
            this.WriteLaggedFrame(stream, Av1TileEncoder.FromPackedTiles(picture, this.SymbolEncoder), writeSequenceHeader, writeTemporalDelimiter);
            this.RecordCodedLoopFilterLevels();
            this.CompleteLaggedGlobalMotion(frame.UpdateType);
            this.CompleteSegmentation(current, picture);
            this.CompleteLaggedMotionVectorStatistics<byte, Av1MotionVectorStatistics.ByteTextureOperator>(parent, source.Frame);
            this.SymbolEncoder.SnapshotTo(current.Context);
            this.MotionField.SaveFrameMotionVectors(picture, current.MotionField);
            this.CompleteFrameHeader();
            this.CompleteReferenceStructure();
            secondPass.CompleteFrame(qIndex, GetLaggedFrameBits(stream, start, writeTemporalDelimiter));
            temporalModel?.CompleteFrame(in frame, source.Frame, picture, current.Context, qIndex);

            current.Buffer.Frame.ExtendBorders();
            this.referencePool.Refresh(current, frameHeader.RefreshFrameFlags);
            this.RefreshFilmGrain();
        }
    }

    private sealed partial class HighBitDepthSequenceEncoder : SequenceEncoder.ILaggedFrameCoder<ushort>
    {
        /// <inheritdoc/>
        public Av1EncoderReferencePool<ushort> ReferencePool => this.referencePool;

        /// <inheritdoc/>
        public override void EncodeWithLookahead<TPixel>(
            Image<TPixel> image,
            int firstFrameIndex,
            int frameCount,
            long frameDurationTicks,
            Stream stream,
            Span<long> sampleEnds,
            Span<bool> syncSamples,
            CancellationToken cancellationToken)
            => this.EncodeLagged<TPixel, ushort, HeifUShortSampleConverter, Av1FirstPassOperator.UInt16Operator, Av1TemporalFilter.UInt16Operator, Av1MotionSearchBase.UInt16Operator, Av1TplUInt16Operator>(
                this, image, firstFrameIndex, frameCount, frameDurationTicks, stream, sampleEnds, syncSamples, cancellationToken);

        /// <inheritdoc/>
        public bool DecideLaggedScreenContent(Av1EncoderFrameBuffer<ushort> unfilteredSource, bool isKeyFrame)
        {
            if (isKeyFrame)
            {
                DecideScreenContent(unfilteredSource.Frame, this.SequenceHeader, this.Options, ref this.ScreenContent);
            }

            return this.ScreenContent.IsScreenContent;
        }

        /// <inheritdoc/>
        public void EncodeLaggedFrame(
            Av1EncoderFrameBuffer<ushort> source,
            in Av1SecondPassFrame frame,
            Av1SecondPass secondPass,
            Stream stream,
            bool writeTemporalDelimiter,
            ILaggedTemporalModel<ushort>? temporalModel,
            Av1EncoderFrameBuffer<ushort>? lastSource)
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            this.ConfigureLaggedFrameHeader(in frame);

            Av1EncoderReferencePool<ushort>.Entry current = this.referencePool.Acquire();
            ApplyScreenContentTools(
                this.SequenceHeader, frameHeader, this.Options, new Size(source.Frame.Width, source.Frame.Height), this.ScreenContent);

            bool isScreenContent = this.ScreenContent.IsScreenContent;

            this.DecideIntegerMotionVectors<ushort, Av1IntraSuperblockEncoder.UInt16Operator>(source.Frame, lastSource?.Frame);

            this.PictureBuffer.Reset(frameHeader);
            Av1PictureParentControlSet parent = this.PictureBuffer.Picture.Parent;
            parent.RecodesFrame = false;
            parent.PreviousSource = default;
            parent.SourceBlockSad = default;
            parent.HighSourceSad = false;
            parent.FrameSourceSad = 0;
            parent.SourceMotionPercentage = 0;

            // The rate control keeps the key and golden counters. Reference: rc->frames_since_key and
            // rc->frames_since_golden.
            parent.FramesSinceKey = secondPass.FramesSinceKey;
            parent.FramesSinceGolden = secondPass.FramesSinceGolden;
            parent.IsScreenContent = isScreenContent;
            parent.IsGraphicsAnimation = frame.IsGraphicsAnimation;

            this.ConfigureLaggedReferenceStructure(parent, in frame);

            // Reference distances follow display order, and a hidden frame keeps the count of the frames shown
            // before it. Reference: current_frame->display_order_hint and current_frame->frame_number.
            this.BlockWorkspace.EncodedFrameCount = frame.DisplayOrder;
            this.BlockWorkspace.FrameNumber = frame.DisplayOrder - frame.SourceOffset;

            // The model statistics of the frame feed its quantizer choice. Reference: process_tpl_stats_frame() before
            // av1_rc_pick_q_and_bounds() in set_size_dependent_vars().
            double qStepRatio = 1;
            bool tplFrameValid = false;
            bool tplReady = temporalModel?.ApplyToFrame(parent, frame.GroupIndex, out qStepRatio, out tplFrameValid) ?? false;
            if (temporalModel is null)
            {
                parent.TplFrame = null;
                parent.TplStatisticsReady = false;
            }

            int qIndex = secondPass.ChooseBaseQIndex(isScreenContent, tplReady, tplFrameValid, parent.TplImportance, qStepRatio);
            this.ApplyLaggedQuantizer(qIndex);

            // The lookahead makes this a statistics-consuming stage, so inter frames scale the rate multiplier by
            // their layer depth and the golden boost. Reference: is_stat_consumption_stage() in av1_compute_rd_mult().
            parent.IsStatConsumptionStage = true;
            parent.LayerDepth = frame.LayerDepth;
            parent.GoldenBoost = secondPass.GoldenBoost;

            parent.EncoderOptions = this.Options;
            parent.EncoderBorder = this.GetEncoderBorder();
            parent.ConstantQualityIndex = GetConstantQualityLevel(this.Options, this.ConstantQualityIndex);
            parent.SpeedSettings = new(
                this.Options.Speed,
                this.Options.IsAllIntra,
                frameHeader.IsIntra,
                parent.FrameUpdateType,
                this.QIndex,
                new Size(source.Frame.Width, source.Frame.Height),
                sharpness: this.Options.Sharpness,
                tuning: this.Options.Tuning);

            parent.BorderPad = this.UsesBorderPad;
            this.BeginLaggedMotionVectorStatistics(in frame, parent, new Size(source.Frame.Width, source.Frame.Height));

            this.ConfigureReferenceTools(parent);
            this.ResetIntraSegmentation();

            // Reference: the av1_determine_sc_tools_with_encoding() call of encode_with_recode_loop(), which a
            // lookahead sequence reaches because its statistics allow recoding.
            isScreenContent = this.DetermineScreenContentWithEncoding<ushort, Av1IntraSuperblockEncoder.UInt16Operator>(
                source.Frame,
                current.Buffer.Frame,
                qIndex,
                secondPass.BestQuality == 0 && secondPass.WorstQuality == 0,
                isScreenContent);

            parent.IsScreenContent = isScreenContent;
            this.CommonBaseQIndex = frameHeader.QuantizationParameters.BaseQIndex;
            this.SymbolEncoder.BeginFrame(this.BindReferences(parent), frameHeader.QuantizationParameters.BaseQIndex);
            this.BeginLaggedGlobalMotion(in frame, secondPass.Group);
            this.SearchGlobalMotion<ushort, UInt16GlobalMotionSearchOperator>(source.Frame, this.references, parent);
            this.BeginSegmentation(
                this.referencePool,
                current,
                parent,
                allowsRecode: true,
                frame.MacroblockAverageEnergy,
                secondPass.BestQuality,
                secondPass.WorstQuality,
                Av1RateControl.GetSuperblockTargetRate(secondPass.FrameTarget, source.Frame.Width, source.Frame.Height));

            bool writeSequenceHeader = frameHeader.FrameType == ObuFrameType.KeyFrame && frameHeader.ShowFrame;
            Av1PictureControlSet picture = this.PictureBuffer.Picture;
            qIndex = this.AnalyzeLaggedFrame<ushort, Av1IntraSuperblockEncoder.UInt16Operator, Av1MotionVectorStatistics.UInt16TextureOperator, UInt16GlobalMotionSearchOperator>(
                secondPass,
                in frame,
                parent,
                source.Frame,
                this.references,
                this.searchReferences,
                this.referencePool,
                current,
                writeSequenceHeader,
                qIndex,
                out bool switchableBeforeFix);

            this.PrepareFilmGrain();
            Av1TileEncoder.CompleteFrame<ushort, Av1IntraSuperblockEncoder.UInt16Operator,
                Av1DeblockingFilter.VerticalUInt16EdgeOperator, Av1DeblockingFilter.HorizontalUInt16EdgeOperator, Av1CdefEncoder.UInt16Operator>(
                this.SymbolEncoder,
                source.Frame,
                this.references,
                this.searchReferences,
                current.Buffer.Frame,
                picture,
                this.Coefficients,
                this.TileWorkspace,
                this.BlockWorkspace,
                switchableBeforeFix);

            long start = stream.Length;
            this.WriteLaggedFrame(stream, Av1TileEncoder.FromPackedTiles(picture, this.SymbolEncoder), writeSequenceHeader, writeTemporalDelimiter);
            this.RecordCodedLoopFilterLevels();
            this.CompleteLaggedGlobalMotion(frame.UpdateType);
            this.CompleteSegmentation(current, picture);
            this.CompleteLaggedMotionVectorStatistics<ushort, Av1MotionVectorStatistics.UInt16TextureOperator>(parent, source.Frame);
            this.SymbolEncoder.SnapshotTo(current.Context);
            this.MotionField.SaveFrameMotionVectors(picture, current.MotionField);
            this.CompleteFrameHeader();
            this.CompleteReferenceStructure();
            secondPass.CompleteFrame(qIndex, GetLaggedFrameBits(stream, start, writeTemporalDelimiter));
            temporalModel?.CompleteFrame(in frame, source.Frame, picture, current.Context, qIndex);

            current.Buffer.Frame.ExtendBorders();
            this.referencePool.Refresh(current, frameHeader.RefreshFrameFlags);
            this.RefreshFilmGrain();
        }
    }
}
