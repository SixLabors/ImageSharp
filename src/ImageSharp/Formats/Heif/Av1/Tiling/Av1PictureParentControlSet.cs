// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;
using SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;
using SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

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
    /// Gets or sets a value indicating whether source analysis classifies this frame as screen content.
    /// </summary>
    public bool IsScreenContent { get; set; }

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
