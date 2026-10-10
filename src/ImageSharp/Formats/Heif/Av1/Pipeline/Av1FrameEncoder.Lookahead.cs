// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Lookahead;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Quantizers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.TemporalFilter;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Components;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <content>
/// Codes a good-quality sequence through the lookahead. The frames of each golden group leave display order. The encoder codes the alternate
/// references hidden and shows them later. Each temporal unit ends with a shown frame.
/// </content>
internal static partial class Av1FrameEncoder
{
    /// <summary>
    /// The time-stamp ticks per second.
    /// </summary>
    private const long TicksPerSecond = 10000000;

    /// <summary>
    /// The size of an empty temporal delimiter: its OBU header and a zero size. The rate control does not count the delimiter as part of the frame.
    /// </summary>
    private const int TemporalDelimiterBytes = 2;

    internal abstract partial class SequenceEncoder
    {
        /// <summary>
        /// Whether each update type found a global motion model in the current golden group, or <see cref="int.MaxValue"/> before its first frame.
        /// </summary>
        private readonly int[] validGlobalMotionFound = new int[(int)Av1FrameUpdateType.Count];

        /// <summary>
        /// The motion vector statistics of the last coded frame.
        /// </summary>
        private readonly Av1MotionVectorStatistics motionVectorStatistics = new();

        /// <summary>
        /// Whether the statistics of earlier frames turn the global motion search of the current frame off.
        /// </summary>
        private bool globalMotionDisabledByStatistics;

        /// <summary>
        /// The stream that holds a packed frame to measure its size before the recode decision. The encoder creates it on first use.
        /// </summary>
        private MemoryStream? measuredFrameStream;

        /// <summary>
        /// Gets the constant-quality index of the sequence.
        /// </summary>
        private protected int ConstantQualityIndex => this.constantQualityIndex;

        /// <summary>
        /// Gets the number of times the lookahead coded a frame again because its size missed the bit target.
        /// </summary>
        internal int RecodedFrameCount { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the encoder replaces residuals outside the visible frame. Good-quality usage below speed 7 does this when
        /// the delta-q mode is objective, adaptive quantization is off, and the sharpness is not 3.
        /// </summary>
        private protected bool UsesBorderPad =>
            !this.Options.IsAllIntra &&
            this.Options.Speed < HeifEncodingSpeed.Level7 &&
            this.Options.DeltaQMode == Av1DeltaQMode.Objective &&
            this.Options.AdaptiveQuantizationMode == Av1AdaptiveQuantizationMode.None &&
            this.Options.Sharpness != 3;

        /// <summary>
        /// Encodes the frames of a sequence with the lookahead of <see cref="Av1EncoderOptions.LagInFrames"/> and records where each sample ends.
        /// For each input frame, the loop pushes the frame, runs the lookahead stage, and codes frames until one is shown.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <param name="image">The image that holds the frames.</param>
        /// <param name="firstFrameIndex">The index of the first frame to encode.</param>
        /// <param name="frameCount">The number of frames to encode.</param>
        /// <param name="frameDurationTicks">The frame duration in time-stamp ticks.</param>
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
        /// Returns whether no reference of the current inter frame follows it in display order while the sequence codes with a lookahead.
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
        /// Analyzes a lagged frame. Under a bit budget, the method packs each coding to measure its size. While the size misses its target, it codes
        /// the frame again at the quantizer that the rate control picks. A discarded coding still moves the frame probabilities, and the next coding
        /// starts from a clean picture. The frame before a forced key frame records its unfiltered error. After this method, the frame is ready for
        /// its loop filters and final pack.
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
            where TGlobalMotionOperator : struct, Av1GlobalMotionSearch.IAv1GlobalMotionOperator<TSample>
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            Av1PictureControlSet picture = this.PictureBuffer.Picture;
            Size frameSize = new(source.Width, source.Height);
            int superblockTargetRate = Av1RateControl.GetSuperblockTargetRate(secondPass.FrameTarget, source.Width, source.Height);
            switchableBeforeFix = Av1TileEncoder.AnalyzeFrame<TSample, TOperator>(
                this.SymbolEncoder, source, references, searchReferences, current.Buffer.Frame, picture, this.Coefficients, this.TileWorkspace, this.BlockWorkspace);

            while (secondPass.UsesRecodeLoop)
            {
                // Each measuring pack finalizes the frame, and this steps the film grain seed.
                this.PrepareFilmGrain();
                Av1TileEncoder.PackFrame<TSample, TOperator>(
                    this.SymbolEncoder, source, references, searchReferences, current.Buffer.Frame, picture, this.Coefficients, this.TileWorkspace, this.BlockWorkspace);

                long packedBits = this.MeasureLaggedFrame(Av1TileEncoder.FromPackedTiles(picture, this.SymbolEncoder), writeSequenceHeader);

                // Each coding gathers its motion vector statistics. The next coding reads them.
                this.CompleteLaggedMotionVectorStatistics<TSample, TTextureOperator>(parent, source);
                long keyFrameError = secondPass.MatchesAmbientError
                    ? GetLumaSquaredError<TSample, TOperator>(source, current.Buffer.Frame)
                    : 0;

                int nextQIndex = qIndex;
                if (!secondPass.UpdateRecodeQuantizer((int)Math.Min(packedBits, int.MaxValue), keyFrameError, ref nextQIndex))
                {
                    break;
                }

                // The discarded coding moved the frame probabilities at the end of its analysis. This update keeps that effect.
                Av1TileEncoder.UpdateFrameProbabilities(picture, this.BlockWorkspace, switchableBeforeFix);
                this.UpdateMotionModeProbabilities(parent);
                this.RecodedFrameCount++;

                qIndex = nextQIndex;
                bool highPrecision = frameHeader.AllowHighPrecisionMotionVector;
                this.BeginLaggedRecode(parent, in frame, qIndex, frameSize);
                this.SymbolEncoder.BeginFrame(this.BindReferences(parent), frameHeader.QuantizationParameters.BaseQIndex);

                // The global motion models depend on the vector precision. If the precision changes, the encoder searches global motion again.
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
        /// Returns the size in bits of the frame OBUs of a packed frame. The size includes the sequence header of a shown key frame, but not the
        /// temporal delimiter.
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
        /// Writes the OBUs of a coded frame. A shown key frame follows a temporal delimiter and the sequence header. The first frame of a temporal
        /// unit follows a temporal delimiter. Any other frame stands alone.
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
        /// Prepares a frame to be coded again at a new quantizer. The picture state starts clean. The frame keeps its probabilities, its motion
        /// search step, and the tools that a coding can only turn off. The quantizer and the speed features follow the new quantizer. The encoder
        /// chooses the motion vector precision again from the statistics of the last coding.
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
                screenContent: false,
                frameSizeScreenContent: null,
                this.Options.Sharpness,
                this.Options.Tuning);

            // Intra block copy stays as the last coding left it. The encoder sets this flag once per frame, and a coding that used no copy turns it off.
            this.ConfigureRecodedReferenceTools(parent);
            this.BeginLaggedMotionVectorStatistics(in frame, parent, frameSize);
            this.CommonBaseQIndex = frameHeader.QuantizationParameters.BaseQIndex;
        }

        /// <summary>
        /// Returns the luma squared error of the visible samples of a reconstruction.
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
        /// </summary>
        /// <param name="stream">The destination stream.</param>
        /// <param name="start">The stream length before the frame.</param>
        /// <param name="wroteTemporalDelimiter">Whether the frame starts its temporal unit.</param>
        /// <returns>The frame size in bits.</returns>
        private protected static long GetLaggedFrameBits(Stream stream, long start, bool wroteTemporalDelimiter)
            => (stream.Length - start - (wroteTemporalDelimiter ? TemporalDelimiterBytes : 0)) * 8;

        /// <summary>
        /// Encodes the frames of a sequence through the lookahead at one sample type. Frames enter the lookahead, and the first pass measures them.
        /// The lookahead decisions order each golden group. The temporal filter and the temporal dependency model run at the start of each group.
        /// Each temporal unit ends with a shown frame.
        /// </summary>
        /// <typeparam name="TPixel">The pixel type.</typeparam>
        /// <typeparam name="TSample">The sample type.</typeparam>
        /// <typeparam name="TStorer">The sample conversion.</typeparam>
        /// <typeparam name="TFilterOperator">The temporal filter arithmetic.</typeparam>
        /// <typeparam name="TSearchOperator">The motion search arithmetic.</typeparam>
        /// <typeparam name="TTplOperator">The first-pass and temporal dependency model arithmetic.</typeparam>
        /// <param name="coder">The frame coder of the sample type.</param>
        /// <param name="image">The image that holds the frames.</param>
        /// <param name="firstFrameIndex">The index of the first frame to encode.</param>
        /// <param name="frameCount">The number of frames to encode.</param>
        /// <param name="frameDurationTicks">The frame duration in time-stamp ticks.</param>
        /// <param name="stream">The destination stream.</param>
        /// <param name="sampleEnds">Receives the stream length after each sample, one per frame.</param>
        /// <param name="syncSamples">Receives whether each sample holds a shown key frame.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private protected void EncodeLagged<TPixel, TSample, TStorer, TFilterOperator, TSearchOperator, TTplOperator>(
            ILaggedFrameCoder<TSample, TFilterOperator, TSearchOperator, TTplOperator> coder,
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
            where TFilterOperator : struct, Av1TemporalFilter.ITemporalFilterOperator<TSample>
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

            using Av1FirstPass<TSample, TTplOperator> firstPass = new(
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

            using LookaheadTemporalFilter<TSample, TFilterOperator, TSearchOperator> filter =
                new(this.Configuration, image.Width, image.Height, bitDepth, colorFormat, lumaBorder, this.Options, this.ConstantQualityIndex);

            // Before the first frame, the filter and the model read the motion settings of a key frame without any quantizer-dependent update.
            // High precision vectors are always allowed.
            Av1MotionSearchSettings keyFrameMotionSettings = new(this.Options.Speed, false, image.Size, -1, true, false, Av1Tuning.Psnr);
            using LookaheadTemporalModel<TSample, TFilterOperator, TSearchOperator, TTplOperator>? temporalModel =
                this.Options.LagInFrames > 1
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
                    // The source joins the lookahead with its border extended. The first pass of the lookahead stage measures it at once.
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
                        // The filter reads the motion settings and frame flags that the encoder holds from the previous frame.
                        Av1MotionSearchSettings motionSettings = codedFrames == 0
                            ? keyFrameMotionSettings
                            : this.PictureBuffer.Picture.Parent.MotionSearchSettings;

                        if (frame.GroupIndex == 0)
                        {
                            // With the model, the lookahead decisions discard the filtered frames of the previous group before the group length test
                            // filters the trial group. Without the model, this code discards them when a new group starts.
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

                        // An intra frame decides its screen content from the unfiltered source. Its quantizer choice, filter and model read the
                        // decision later.
                        Av1EncoderFrameBuffer<TSample> source = lookahead.Peek(frame.SourceOffset)!;
                        bool isScreenContent = coder.DecideLaggedScreenContent(source, frame.IsKeyFrame);
                        secondPass.SetScreenContentType(isScreenContent);

                        // The filter picks the quantizer and the filtered source first.
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

                        // Then the encoder sets the motion search step of a key frame, an alternate reference or a golden frame once. The frame sets
                        // it again later for its own search.
                        if (frame.IsKeyFrame || frame.UpdateType is Av1FrameUpdateType.Alternate or Av1FrameUpdateType.Golden)
                        {
                            this.PrepareMotionSearchStep(frame.IsKeyFrame, image.Size, motionSettings);
                        }

                        // The model measures a new group after the filter processed its frames, so that the model reads the filtered frames.
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
        /// Sets the frame header fields of a frame of a lookahead golden group before its source analysis. These fields are the frame type, the
        /// show flags, and the order hint of its display position.
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
        /// Selects the reference slots, the refreshed slots, and the primary reference of a frame of a lookahead golden group. A key frame also
        /// resets the warped motion and OBMC probabilities to their defaults.
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

            // The update type decides the golden refresh. An alternate reference that resets the references also refreshes golden.
            parent.RefreshesGolden = frame.UpdateType is Av1FrameUpdateType.Key or Av1FrameUpdateType.Golden or
                Av1FrameUpdateType.Overlay || (frame.UpdateType == Av1FrameUpdateType.Alternate && frame.ResetsReferences);

            // The update type decides the alternate reference refresh. An overlay that resets the references also refreshes the alternate reference.
            parent.RefreshesAlternate = frame.UpdateType is Av1FrameUpdateType.Key or Av1FrameUpdateType.Alternate ||
                (frame.UpdateType == Av1FrameUpdateType.Overlay && frame.ResetsReferences);

            if (frameHeader.FrameType == ObuFrameType.KeyFrame)
            {
                this.warpedProbabilities.AsSpan().Fill(64);
                DefaultObmcProbabilities.CopyTo(this.obmcProbabilities, 0);
            }
        }

        /// <summary>
        /// Sets the motion search step of a key frame, an alternate reference or a golden frame once, before the frame sets it again for its own
        /// search. Only the maximum vector magnitude that this call leaves stays in effect. A key frame sets it to the larger frame dimension. An
        /// inter frame discards the maximum of the previous frame, so that its own search starts from the default step.
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
        /// Decides whether the frame searches global motion from the models that its golden group found so far. The first frame of a group clears
        /// the record. A group with an alternate reference stops the search after its alternate, intermediate alternate and leaf frames all coded
        /// without a model.
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
        /// Chooses the motion vector precision of an inter frame from its quantizer and the statistics of the last coded frame. Then the method
        /// discards those statistics. At the speeds that read the statistics, the packing pass then collects the statistics of this frame.
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
        /// Completes the motion vector statistics that the packing pass collected from a frame. This step adds the texture sums of the source.
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
        /// Records whether the coded frame found a global motion model for its update type.
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
        /// Applies the quantizer that the lookahead chose for the frame.
        /// </summary>
        /// <param name="qIndex">The base quantizer index.</param>
        private protected void ApplyLaggedQuantizer(int qIndex)
        {
            this.QIndex = qIndex;
            ApplyFrameQuantizer(this.FrameHeader, this.SequenceHeader, qIndex, this.Options);

            // The delta quantizer of the first superblock of each tile is relative to the frame quantizer. The picture reset read the quantizer before
            // this frame chose it, so the code sets the new value here.
            this.PictureBuffer.Picture.Parent.PreviousQIndex.Span.Fill(qIndex);
        }

        /// <summary>
        /// Writes a frame header that shows the coded frame of a display position. The references do not change.
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

    private sealed partial class SampleSequenceEncoder<
        TSample,
        TStorer,
        TFilterOperator,
        TSearchOperator,
        TTplOperator,
        TBlockOperator,
        TGlobalMotionOperator,
        TTextureOperator,
        TVerticalEdgeOperator,
        THorizontalEdgeOperator,
        TCdefOperator> : SequenceEncoder.ILaggedFrameCoder<TSample, TFilterOperator, TSearchOperator, TTplOperator>
    {
        /// <inheritdoc/>
        public Av1EncoderReferencePool<TSample> ReferencePool => this.referencePool;

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
            => this.EncodeLagged<TPixel, TSample, TStorer, TFilterOperator, TSearchOperator, TTplOperator>(
                this, image, firstFrameIndex, frameCount, frameDurationTicks, stream, sampleEnds, syncSamples, cancellationToken);

        /// <inheritdoc/>
        public bool DecideLaggedScreenContent(Av1EncoderFrameBuffer<TSample> unfilteredSource, bool isKeyFrame)
        {
            if (isKeyFrame)
            {
                DecideScreenContent(unfilteredSource.Frame, this.SequenceHeader, this.Options, ref this.ScreenContent);
            }

            return this.ScreenContent.IsScreenContent;
        }

        /// <inheritdoc/>
        public void EncodeLaggedFrame(
            Av1EncoderFrameBuffer<TSample> source,
            in Av1SecondPassFrame frame,
            Av1SecondPass secondPass,
            Stream stream,
            bool writeTemporalDelimiter,
            LookaheadTemporalModel<TSample, TFilterOperator, TSearchOperator, TTplOperator>? temporalModel,
            Av1EncoderFrameBuffer<TSample>? lastSource)
        {
            ObuFrameHeader frameHeader = this.FrameHeader;
            this.ConfigureLaggedFrameHeader(in frame);

            Av1EncoderReferencePool<TSample>.Entry current = this.referencePool.Acquire();
            ApplyScreenContentTools(
                this.SequenceHeader, frameHeader, this.Options, new Size(source.Frame.Width, source.Frame.Height), this.ScreenContent);

            bool isScreenContent = this.ScreenContent.IsScreenContent;

            this.DecideIntegerMotionVectors<TSample, TBlockOperator>(source.Frame, lastSource?.Frame);

            this.PictureBuffer.Reset(frameHeader);
            Av1PictureParentControlSet parent = this.PictureBuffer.Picture.Parent;
            parent.RecodesFrame = false;
            parent.PreviousSource = default;
            parent.SourceBlockSad = default;
            parent.HighSourceSad = false;
            parent.FrameSourceSad = 0;

            // The rate control keeps the counters of frames since the last key frame and since the last golden frame.
            parent.FramesSinceKey = secondPass.FramesSinceKey;
            parent.FramesSinceGolden = secondPass.FramesSinceGolden;
            parent.IsScreenContent = isScreenContent;
            parent.IsGraphicsAnimation = frame.IsGraphicsAnimation;

            this.ConfigureLaggedReferenceStructure(parent, in frame);

            // Reference distances follow display order. A hidden frame keeps the count of the frames shown before it.
            this.BlockWorkspace.EncodedFrameCount = frame.DisplayOrder;
            this.BlockWorkspace.FrameNumber = frame.DisplayOrder - frame.SourceOffset;

            // The model statistics of the frame feed its quantizer choice, so the frame takes them first.
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

            // The lookahead makes this a statistics-consuming stage. Thus inter frames scale the rate multiplier by their layer depth and the golden
            // boost.
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
                screenContent: false,
                frameSizeScreenContent: null,
                this.Options.Sharpness,
                this.Options.Tuning);

            parent.BorderPad = this.UsesBorderPad;
            this.BeginLaggedMotionVectorStatistics(in frame, parent, new Size(source.Frame.Width, source.Frame.Height));

            this.ConfigureReferenceTools(parent);
            this.ResetIntraSegmentation();

            // A lookahead sequence has statistics that allow recoding. Thus a key frame can test the screen content tools with two trial encodes.
            isScreenContent = this.DetermineScreenContentWithEncoding<TSample, TBlockOperator>(
                source.Frame,
                current.Buffer.Frame,
                qIndex,
                secondPass.BestQuality == 0 && secondPass.WorstQuality == 0,
                isScreenContent);

            parent.IsScreenContent = isScreenContent;
            this.CommonBaseQIndex = frameHeader.QuantizationParameters.BaseQIndex;
            this.SymbolEncoder.BeginFrame(this.BindReferences(parent), frameHeader.QuantizationParameters.BaseQIndex);
            this.BeginLaggedGlobalMotion(in frame, secondPass.Group);
            this.SearchGlobalMotion<TSample, TGlobalMotionOperator>(source.Frame, this.references, parent);
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
            qIndex = this.AnalyzeLaggedFrame<TSample, TBlockOperator, TTextureOperator, TGlobalMotionOperator>(
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
            Av1TileEncoder.CompleteFrame<TSample, TBlockOperator, TVerticalEdgeOperator, THorizontalEdgeOperator, TCdefOperator>(
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
            this.CompleteLaggedMotionVectorStatistics<TSample, TTextureOperator>(parent, source.Frame);
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
