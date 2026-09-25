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
}
