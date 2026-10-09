// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

/// <summary>
/// Holds encoder state that is shared by all coding passes for one AV1 picture.
/// </summary>
internal class Av1PictureParentControlSet
{
    /// <summary>
    /// Gets or sets frame dimensions and tile state shared by encoder stages.
    /// </summary>
    public required Av1EncoderCommon Common { get; set; }

    /// <summary>
    /// Gets or sets the frame header being encoded.
    /// </summary>
    public required ObuFrameHeader FrameHeader { get; set; }

    /// <summary>
    /// Gets or sets the preceding quantizer index for each tile context.
    /// </summary>
    public required Memory<int> PreviousQIndex { get; set; }

    /// <summary>
    /// Gets or sets the current picture's reference update role.
    /// </summary>
    public Av1FrameUpdateType FrameUpdateType { get; set; }

    /// <summary>
    /// Gets a value indicating whether the frame codes the source of an alternate reference. Overlay and intermediate overlay frames do this.
    /// </summary>
    public bool IsSourceAlternateReference =>
        this.FrameUpdateType is Av1FrameUpdateType.Overlay or Av1FrameUpdateType.IntermediateOverlay;

    /// <summary>
    /// Gets or sets the selected-transform counts borrowed from the encoder workspace.
    /// </summary>
    public Memory<int> TransformTypeCounts { get; set; }

    /// <summary>
    /// Gets or sets interpolation context counts borrowed for the current picture.
    /// </summary>
    public Memory<int> InterpolationCounts { get; set; }

    /// <summary>
    /// Gets or sets emitted interpolation symbol counts borrowed for the current picture.
    /// </summary>
    public Memory<int> SelectedInterpolationCounts { get; set; }

    /// <summary>
    /// Gets the counts of coded blocks that can use warped motion. Entry 0 counts the blocks that do not use it. Entry 1 counts the blocks that use it.
    /// </summary>
    public int[] WarpedUsage { get; } = new int[2];

    /// <summary>
    /// Gets the OBMC probability of each block size for the update type of the frame.
    /// </summary>
    public int[] ObmcProbabilities { get; } = new int[(int)Av1BlockSize.AllSizes];

    /// <summary>
    /// Gets the counts of blocks that can use OBMC, two entries per block size. The first entry counts the blocks that do not use OBMC.
    /// The second entry counts the blocks that use it.
    /// </summary>
    public int[] ObmcUsage { get; } = new int[(int)Av1BlockSize.AllSizes * 2];

    /// <summary>
    /// Gets the display distance of each reference type from the frame. The distance is negative for a past reference and zero for a disabled one.
    /// </summary>
    public int[] ReferenceDistances { get; } = new int[Av1Constants.ReferenceFrameCount + 1];

    /// <summary>
    /// Gets or sets the references whose single-reference modes the block-level pruning keeps, one bit per reference type.
    /// </summary>
    public int KeepSingleReferenceMask { get; set; }

    /// <summary>
    /// Gets or sets the references whose compound pairs the block-level pruning keeps, one bit per reference type.
    /// </summary>
    public int KeepCompoundReferenceMask { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether every reference precedes the frame in display order.
    /// </summary>
    public bool AllOneSidedReferences { get; set; }

    /// <summary>
    /// Gets a value indicating whether the frame drops every compound reference pair. This occurs when one-sided compound is disabled and every
    /// reference lies on one side.
    /// </summary>
    public bool PrunesAllCompoundReferences => this.AllOneSidedReferences && this.SpeedSettings.DisableOneSidedCompound;

    /// <summary>
    /// Gets or sets the closest enabled reference before the current frame.
    /// </summary>
    public Av1ReferenceFrameType NearestPastReference { get; set; }

    /// <summary>
    /// Gets or sets the enabled prediction references, with bit positions matching their identifiers.
    /// </summary>
    public byte AvailableReferenceMask { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an inter frame starts a golden group and refreshes GOLDEN.
    /// </summary>
    public bool StartsGoldenGroup { get; set; }

    /// <summary>
    /// Gets or sets the temporal motion field of a sequence frame, or <see langword="null"/> for a still picture.
    /// </summary>
    public Av1EncoderMotionField? MotionField { get; set; }

    /// <summary>
    /// Gets or sets the closest enabled reference after the current frame.
    /// </summary>
    public Av1ReferenceFrameType NearestFutureReference { get; set; }

    /// <summary>
    /// Gets or sets the encoder palette-search level.
    /// </summary>
    public int PaletteLevel { get; set; }

    /// <summary>
    /// Gets or sets the native-valued encoding speed.
    /// </summary>
    public HeifEncodingSpeed EncodingSpeed { get; set; }

    /// <summary>
    /// Gets or sets the resolved speed-feature policy for this picture.
    /// </summary>
    public Av1EncoderSpeedSettings SpeedSettings { get; set; }

    /// <summary>
    /// Gets or sets the encoder configuration of this picture.
    /// </summary>
    public Av1EncoderOptions EncoderOptions { get; set; } = Av1EncoderOptions.Create(HeifEncodingSpeed.Level6);

    /// <summary>
    /// Gets or sets the configured quantizer index of constant-quality rate control.
    /// </summary>
    public int ConstantQualityIndex { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether any superblock used a quantizer other than the frame quantizer.
    /// </summary>
    public bool DeltaQUsed { get; set; }

    /// <summary>
    /// Gets or sets the quantizer delta of the last superblock that chose one. Later frames keep this value until another superblock chooses one.
    /// </summary>
    public int SuperblockDeltaQIndex { get; set; }

    /// <summary>
    /// Gets or sets the temporal dependency statistics of the frame, or <see langword="null"/> when the encoder does not run the temporal
    /// dependency model for it. The encoder reads the statistics only when <see cref="TplStatisticsReady"/> is set.
    /// </summary>
    public Av1TplFrameStatistics? TplFrame { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the temporal dependency model measured valid statistics for the frame in the current golden group.
    /// </summary>
    public bool TplStatisticsReady { get; set; }

    /// <summary>
    /// Gets or sets the importance of the frame from its temporal dependency statistics. The value is the exponential of a mean log ratio,
    /// weighted by source distortion. The ratio divides the reconstruction cost by the sum of the reconstruction and dependency costs.
    /// </summary>
    public double TplImportance { get; set; }

    /// <summary>
    /// Gets or sets the golden boost of the group of the frame, after the encoder combined it with the temporal dependency boost.
    /// </summary>
    public int GoldenBoost { get; set; }

    /// <summary>
    /// Gets or sets the pyramid layer depth of the frame in its golden group.
    /// </summary>
    public int LayerDepth { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the rate multipliers apply the layer depth and golden boost adjustments.
    /// One-pass good-quality coding with a lookahead sets it.
    /// </summary>
    public bool IsStatConsumptionStage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether each coding block derives its rate multiplier from the superblock quantizer. When temporal
    /// dependency statistics are ready, the block importance also changes the multiplier. The tile encoder sets this value before analysis.
    /// </summary>
    public bool CodingBlockDeltaRateMultiplier { get; set; }

    /// <summary>
    /// Gets a value indicating whether the temporal dependency model supplies statistics for the update type of the frame.
    /// Key frames, golden frames and alternate references get statistics.
    /// </summary>
    public bool IsTplEligible =>
        this.FrameUpdateType is Av1FrameUpdateType.Key or Av1FrameUpdateType.Golden or Av1FrameUpdateType.Alternate;

    /// <summary>
    /// Gets or sets the rate multiplier scaling factor of each 16x16 luma block for the SSIM and image tunes, or <see langword="null"/> for the
    /// other tunes.
    /// </summary>
    public double[]? SsimRateMultiplierFactors { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the sequence encoder measured <see cref="SsimRateMultiplierFactors"/> before it resized the source.
    /// When this value is set, frame encoding keeps these factors.
    /// </summary>
    public bool HasPrecomputedSsimRateMultiplierFactors { get; set; }

    /// <summary>
    /// Gets or sets the frame border of the encoder configuration, in luma samples. This border limits the projection motion search.
    /// It is a complete superblock plus 32 samples, or 288 samples while a fixed resize mode is set.
    /// </summary>
    public int EncoderBorder { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the frame has the screen content type. The source detection of the last intra frame sets it.
    /// The screen content trial of a key frame can also set it. The rate control and the adaptive quantization read it.
    /// It is not the screen content tune, which the encoder never sets.
    /// </summary>
    public bool IsScreenContent { get; set; }

    /// <summary>
    /// Gets or sets the block size that every superblock splits into without a partition search, or <see cref="Av1BlockSize.Invalid"/> to search
    /// the partition.
    /// </summary>
    public Av1BlockSize FixedPartitionSize { get; set; } = Av1BlockSize.Invalid;

    /// <summary>
    /// Gets or sets the number of luma samples in the finally coded blocks that use a luma palette.
    /// </summary>
    public int PalettePixelCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the next frame preparation keeps the frame probabilities that an earlier coding of the same frame
    /// updated. When this value is not set, a key frame restores the probabilities.
    /// </summary>
    public bool RetainsFrameProbabilities { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the encoder codes the frame again at a new quantizer, because its size missed the bit target.
    /// When this value is set, the frame preparation keeps the motion search step and vector magnitude of the first coding.
    /// </summary>
    public bool RecodesFrame { get; set; }

    /// <summary>
    /// Gets or sets whether the frame allowed the screen content tools before its screen content trial, or <see langword="null"/> when no trial
    /// changed them. The frame-size speed features keep this value.
    /// </summary>
    public bool? ScreenContentToolsBeforeTrial { get; set; }

    /// <summary>
    /// Gets or sets the quantizer of the screen content trial of the frame, or -1 when the frame ran no trial.
    /// The trial updates the quantizer-dependent speed features before the frame does.
    /// </summary>
    public int ScreenContentTrialQIndex { get; set; } = -1;

    /// <summary>
    /// Gets or sets a value indicating whether the look-ahead statistics classify the frame as graphics or animation.
    /// </summary>
    public bool IsGraphicsAnimation { get; set; }

    /// <summary>
    /// Gets or sets the preceding eight-bit source planes, borrowed for temporal source analysis and filtering.
    /// </summary>
    public Av1EncoderFrame<byte>.PlanarView PreviousSource { get; set; }

    /// <summary>
    /// Gets or sets the smoothed quantizer index of ordinary inter frames.
    /// </summary>
    public int AverageInterQuantizer { get; set; } = 127;

    /// <summary>
    /// Gets or sets temporal source SADs for the frame's 64x64 blocks in raster order.
    /// </summary>
    public Memory<ulong> SourceBlockSad { get; set; }

    /// <summary>
    /// Gets or sets the mean temporal source SAD over 64x64 blocks.
    /// </summary>
    public ulong FrameSourceSad { get; set; }

    /// <summary>
    /// Gets or sets the running mean temporal source SAD, including the current frame.
    /// </summary>
    public ulong AverageSourceSad { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether temporal source error rose sharply above its running average.
    /// </summary>
    public bool HighSourceSad { get; set; }

    /// <summary>
    /// Gets or sets the area, in 4x4 units, coded with less than one sample of LAST-frame motion.
    /// </summary>
    public int LowMotionArea { get; set; }

    /// <summary>
    /// Gets or sets the running percentage of low-motion area in preceding inter frames.
    /// </summary>
    public int AverageFrameLowMotion { get; set; }

    /// <summary>
    /// Gets or sets the number of encoded frames since the preceding key frame.
    /// </summary>
    public int FramesSinceKey { get; set; }

    /// <summary>
    /// Gets or sets the number of displayed inter frames since the golden reference was refreshed.
    /// </summary>
    public int FramesSinceGolden { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the frame refreshes the golden reference.
    /// </summary>
    public bool RefreshesGolden { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the frame refreshes the alternate reference.
    /// </summary>
    public bool RefreshesAlternate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether variance adaptive quantization chooses the segment of each block from its source variance in this
    /// frame. When this value is not set, the segment comes from the previous map.
    /// </summary>
    public bool VarianceSegmentRefresh { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether complexity adaptive quantization chooses the segment of each searched block in this frame.
    /// </summary>
    public bool ComplexitySegmentRefresh { get; set; }

    /// <summary>
    /// Gets or sets the target rate of the frame per 64x64 area. Complexity adaptive quantization compares the rate of a block with this value.
    /// </summary>
    public int SuperblockTargetRate { get; set; }

    /// <summary>
    /// Gets or sets the cyclic refresh of a real-time sequence that uses it, or <see langword="null"/>.
    /// </summary>
    public Av1CyclicRefresh? CyclicRefresh { get; set; }

    /// <summary>
    /// Gets or sets the segment map of the primary reference frame. The map is empty when the frame has no primary reference frame, or when that
    /// reference frame did not use segmentation.
    /// </summary>
    public ReadOnlyMemory<byte> PreviousSegmentMap { get; set; }

    /// <summary>
    /// Gets or sets the segment map that the encoder keeps across frames. A frame that updates its map reads it, except when it refreshes its
    /// variance segments. Skipped blocks write their predicted segment. Complexity adaptive quantization writes each searched block.
    /// It also reads the segment that each coded block takes.
    /// </summary>
    public Memory<byte> EncoderSegmentMap { get; set; }

    /// <summary>
    /// Gets or sets the segment map that the frame buffer holds during the frame search. Until the encoder writes the bitstream, it holds the map
    /// that the previous frame left. Only cyclic refresh writes it during the search.
    /// </summary>
    public Memory<byte> SearchSegmentMap { get; set; }

    /// <summary>
    /// Gets or sets the noise estimate of a real-time sequence that uses it, or <see langword="null"/>.
    /// </summary>
    public Av1NoiseEstimate? NoiseEstimate { get; set; }

    /// <summary>
    /// Gets the noise level that cyclic refresh reads. It is the level of the last completed estimate, or the lowest level when the estimate is off.
    /// </summary>
    public int NoiseLevel => this.NoiseEstimate is { Enabled: true } noiseEstimate ? noiseEstimate.Level : Av1NoiseEstimate.LowestLevel;

    /// <summary>
    /// Gets the noise level of the running estimate, or the low level when the estimate is off.
    /// </summary>
    public int RunningNoiseLevel => this.NoiseEstimate is { Enabled: true } noiseEstimate ? noiseEstimate.ExtractLevel() : Av1NoiseEstimate.LowLevel;

    /// <summary>
    /// Gets or sets the estimated rate to code the segment identifiers with spatial prediction.
    /// </summary>
    public long SpatialSegmentCost { get; set; }

    /// <summary>
    /// Gets or sets the estimated rate to code the segment identifiers against the map of the primary reference frame.
    /// </summary>
    public long TemporalSegmentCost { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether distortion stops at the frame edge rather than at the coded eight-sample boundary.
    /// When this value is set, the visible part of the residual also fills the residual beyond the frame edge. Good-quality sequences set it.
    /// </summary>
    public bool BorderPad { get; set; }

    /// <summary>
    /// Gets or sets the control that adjusts the refreshed slots after the frame is coded, or <see langword="null"/> to keep them.
    /// </summary>
    public IAv1ReferenceRefreshControl? ReferenceRefreshControl { get; set; }

    /// <summary>
    /// Gets or sets the resolved motion-search policy for the current frame.
    /// </summary>
    public Av1MotionSearchSettings MotionSearchSettings { get; set; }

    /// <summary>
    /// Gets or sets the initial full-pixel search step derived before the frame's first block.
    /// </summary>
    public int MotionSearchStepParameter { get; set; }

    /// <summary>
    /// Gets or sets the largest whole-sample magnitude written by a new-motion mode in the preceding frame.
    /// </summary>
    public int MaximumMotionVectorMagnitude { get; set; } = -1;

    /// <summary>
    /// Gets or sets the motion vector statistics that the packing pass collects for the precision choice of the next frame, or
    /// <see langword="null"/> when the frame does not collect them.
    /// </summary>
    public Av1MotionVectorStatistics? MotionVectorStatistics { get; set; }

    /// <summary>
    /// Returns the rate multiplier of a quantizer for the frame. In the stat consumption stage, a frame that is not a key frame scales the
    /// multiplier by its layer depth and adds its share of the golden boost. In other stages, the multiplier depends only on the quantizer.
    /// </summary>
    /// <param name="qIndex">The quantizer index, including the luma DC delta.</param>
    /// <param name="bitDepth">The sample precision.</param>
    /// <returns>The rate multiplier, at least one.</returns>
    public int GetRateMultiplier(int qIndex, Av1BitDepth bitDepth)
    {
        if (!this.IsStatConsumptionStage)
        {
            return Av1RateDistortion.GetRateMultiplier(
                qIndex, bitDepth, this.FrameUpdateType, this.EncoderOptions.Tuning, this.SpeedSettings.IsRealtime);
        }

        // The stat consumption stage excludes real-time usage, so this path passes no real-time flag.
        return Av1TplRateDistortion.GetRateMultiplier(
            qIndex,
            bitDepth,
            this.FrameUpdateType,
            Math.Min(this.LayerDepth, 6),
            Av1TplRateDistortion.GetBoostIndex(this.GoldenBoost),
            this.FrameHeader.FrameType == ObuFrameType.KeyFrame,
            0,
            true,
            this.EncoderOptions.Tuning);
    }

    /// <summary>
    /// Gets the right and bottom limits of the samples that a distortion measures in one plane. Without border padding, the limits are the coded
    /// eight-sample boundary. With border padding, the limits are the frame edge, and a subsampled plane rounds the distance to that edge up.
    /// </summary>
    /// <param name="subsamplingX">The horizontal subsampling shift of the plane.</param>
    /// <param name="subsamplingY">The vertical subsampling shift of the plane.</param>
    /// <returns>The visible plane width and height.</returns>
    public Size GetVisibleBoundary(int subsamplingX, int subsamplingY)
    {
        int codedWidth = this.Common.ModeInfoColumnCount << Av1Constants.ModeInfoSizeLog2;
        int codedHeight = this.Common.ModeInfoRowCount << Av1Constants.ModeInfoSizeLog2;
        if (!this.BorderPad)
        {
            return new Size(codedWidth >> subsamplingX, codedHeight >> subsamplingY);
        }

        int width = this.FrameHeader.FrameSize.FrameWidth;
        int height = this.FrameHeader.FrameSize.FrameHeight;
        return new Size(
            (codedWidth >> subsamplingX) - ((codedWidth - width + ((1 << subsamplingX) >> 1)) >> subsamplingX),
            (codedHeight >> subsamplingY) - ((codedHeight - height + ((1 << subsamplingY) >> 1)) >> subsamplingY));
    }
}
