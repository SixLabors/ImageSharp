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
    /// Gets a value indicating whether the frame is coded from the source of an alternate reference, as a coded
    /// overlay is. Reference: rc.is_src_frame_alt_ref, which av1_configure_buffer_updates() sets for OVERLAY_UPDATE
    /// and INTNL_OVERLAY_UPDATE.
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
    /// Gets the coded blocks that could use warped motion, split by whether they do. Reference: the warped_used
    /// counts of encode_b().
    /// </summary>
    public int[] WarpedUsage { get; } = new int[2];

    /// <summary>
    /// Gets the OBMC probability of each block size for the frame's update type. Reference: the obmc_probs row of
    /// frame_probs.
    /// </summary>
    public int[] ObmcProbabilities { get; } = new int[(int)Av1BlockSize.AllSizes];

    /// <summary>
    /// Gets the count of blocks that could use OBMC and did not or did, two entries per block size.
    /// Reference: rd_counts.obmc_used.
    /// </summary>
    public int[] ObmcUsage { get; } = new int[(int)Av1BlockSize.AllSizes * 2];

    /// <summary>
    /// Gets the display distance of each reference type from the frame, negative for a past reference and zero for
    /// a disabled one. Reference: ref_relative_dist of set_rel_frame_dist().
    /// </summary>
    public int[] ReferenceDistances { get; } = new int[Av1Constants.ReferenceFrameCount + 1];

    /// <summary>
    /// Gets or sets the references whose single-reference modes the block-level pruning keeps, one bit per
    /// reference type. Reference: keep_single_ref_frame_mask.
    /// </summary>
    public int KeepSingleReferenceMask { get; set; }

    /// <summary>
    /// Gets or sets the references whose compound pairs the block-level pruning keeps, one bit per reference type.
    /// Reference: keep_comp_ref_frame_mask.
    /// </summary>
    public int KeepCompoundReferenceMask { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether every reference precedes the frame in display order.
    /// Reference: all_one_sided_refs from refs_are_one_sided().
    /// </summary>
    public bool AllOneSidedReferences { get; set; }

    /// <summary>
    /// Gets a value indicating whether the frame drops every compound reference pair, because one-sided compound is
    /// disabled and every reference lies on one side. Reference: the first branch of setup_prune_ref_frame_mask().
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
    /// Gets or sets the resolved libaom speed-feature policy for this picture.
    /// </summary>
    public Av1EncoderSpeedSettings SpeedSettings { get; set; }

    /// <summary>
    /// Gets or sets the encoder configuration of this picture.
    /// </summary>
    public Av1EncoderOptions EncoderOptions { get; set; } = Av1EncoderOptions.Create(HeifEncodingSpeed.Level6);

    /// <summary>
    /// Gets or sets the configured quantizer index of constant-quality rate control. Reference: rc_cfg.cq_level.
    /// </summary>
    public int ConstantQualityIndex { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether any superblock used a quantizer other than the frame quantizer.
    /// Reference: deltaq_used.
    /// </summary>
    public bool DeltaQUsed { get; set; }

    /// <summary>
    /// Gets or sets the temporal dependency statistics of the frame, or <see langword="null"/> when the encoder does
    /// not run the temporal dependency model for it. The statistics are read only when
    /// <see cref="TplStatisticsReady"/> is set. Reference: tpl_data->tpl_frame[cpi->gf_frame_index].
    /// </summary>
    public Av1TplFrameStatistics? TplFrame { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the temporal dependency model measured valid statistics for the frame
    /// in the current golden group. Reference: av1_tpl_stats_ready(&amp;cpi->ppi->tpl_data, cpi->gf_frame_index).
    /// </summary>
    public bool TplStatisticsReady { get; set; }

    /// <summary>
    /// Gets or sets the importance of the frame from its temporal dependency statistics: the exponential of the
    /// source-distortion weighted mean log ratio of its reconstruction cost to its reconstruction plus dependency
    /// cost. Reference: cpi->rd.r0, which process_tpl_stats_frame() sets.
    /// </summary>
    public double TplImportance { get; set; }

    /// <summary>
    /// Gets or sets the golden boost of the frame's group, after process_tpl_stats_frame() combined it with the
    /// temporal dependency boost. Reference: cpi->ppi->p_rc.gfu_boost.
    /// </summary>
    public int GoldenBoost { get; set; }

    /// <summary>
    /// Gets or sets the pyramid layer depth of the frame in its golden group. Reference:
    /// cpi->ppi->gf_group.layer_depth[cpi->gf_frame_index].
    /// </summary>
    public int LayerDepth { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the rate multipliers take the layer depth and golden boost
    /// adjustments: one-pass good-quality coding with a lookahead. Reference: is_stat_consumption_stage(cpi), with
    /// cpi->ppi->lap_enabled.
    /// </summary>
    public bool IsStatConsumptionStage { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether each coding block derives its rate multiplier from the superblock
    /// quantizer and, with ready temporal dependency statistics, from its importance. The tile encoder sets it
    /// before analysis. Reference: cpi->cb_delta_rdmult_enabled from enable_delta_rdmult().
    /// </summary>
    public bool CodingBlockDeltaRateMultiplier { get; set; }

    /// <summary>
    /// Gets a value indicating whether the temporal dependency model supplies statistics for the frame's update
    /// type: key frames, golden frames and alternate references. Reference: is_frame_tpl_eligible().
    /// </summary>
    public bool IsTplEligible =>
        this.FrameUpdateType is Av1FrameUpdateType.Key or Av1FrameUpdateType.Golden or Av1FrameUpdateType.Alternate;

    /// <summary>
    /// Gets or sets the rate multiplier scaling factor of each 16x16 luma block for the SSIM and image tunes, or
    /// <see langword="null"/> for the other tunes. Reference: ssim_rdmult_scaling_factors.
    /// </summary>
    public double[]? SsimRateMultiplierFactors { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether source analysis classifies this frame as screen content.
    /// </summary>
    public bool IsScreenContent { get; set; }

    /// <summary>
    /// Gets or sets the size every superblock is partitioned into instead of searching its partition, or
    /// <see cref="Av1BlockSize.Invalid"/> to search it. Reference: the FIXED_PARTITION partition_search_type with
    /// fixed_partition_size.
    /// </summary>
    public Av1BlockSize FixedPartitionSize { get; set; } = Av1BlockSize.Invalid;

    /// <summary>
    /// Gets or sets the number of luma samples in the finally coded blocks that use a luma palette. Reference:
    /// palette_pixel_num.
    /// </summary>
    public int PalettePixelCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the next frame preparation keeps the frame probabilities that an
    /// earlier coding of the same frame updated, instead of restoring them at a key frame. Reference: the single
    /// copy_frame_prob_info() call of encode_with_recode_loop(), before av1_determine_sc_tools_with_encoding().
    /// </summary>
    public bool RetainsFrameProbabilities { get; set; }

    /// <summary>
    /// Gets or sets whether the frame allowed the screen content tools before its screen content trial, which the
    /// frame-size speed features keep, or <see langword="null"/> when no trial changed them. Reference: the
    /// allow_screen_content_tools that set_size_independent_vars() reads before av1_determine_sc_tools_with_encoding().
    /// </summary>
    public bool? ScreenContentToolsBeforeTrial { get; set; }

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
    /// Gets or sets the percentage of source blocks that changed from the preceding frame.
    /// </summary>
    public int SourceMotionPercentage { get; set; }

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
    /// Reference: cpi->refresh_frame.golden_frame.
    /// </summary>
    public bool RefreshesGolden { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether distortion stops at the frame edge rather than at the coded
    /// eight-sample boundary, and the residual beyond the frame edge is filled from its visible part. Good-quality
    /// sequences enable it. Reference: cpi->do_border_pad.
    /// </summary>
    public bool BorderPad { get; set; }

    /// <summary>
    /// Gets or sets the control that adjusts the refreshed slots after the frame is coded, or
    /// <see langword="null"/> to keep them.
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
    /// Returns the rate multiplier of a quantizer for the frame. With stat consumption, a frame other than a key
    /// frame scales it by its layer depth and adds its golden boost share; otherwise it is the multiplier of the
    /// quantizer alone. Reference: av1_compute_rd_mult(), with use_fixed_qp_offsets zero.
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

        // The stat consumption stage excludes real-time usage. Reference: the mode test of
        // is_stat_consumption_stage().
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
    /// Gets the right and bottom limits of the samples that a distortion measures in one plane. Without border
    /// padding they are the coded eight-sample boundary. With it they are the frame edge, and a subsampled plane
    /// rounds the distance to that edge up. Reference: set_pixels_to_frame_edge() and get_visible_dimensions().
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
