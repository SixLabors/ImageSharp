// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline.Tpl;

/// <summary>
/// The encoder state that one run of the model reads: the golden group, the look-ahead sources, the reference slots, the
/// rate control values and the speed features in force. The caller fills it before each call of
/// <see cref="Av1TplModel{TSample, TSearchOperator, TSampleOperator}.SetupStatistics"/>.
/// </summary>
/// <typeparam name="TSample">The unsigned sample storage type.</typeparam>
internal sealed class Av1TplSetupInput<TSample>
    where TSample : unmanaged
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Av1TplSetupInput{TSample}"/> class.
    /// </summary>
    public Av1TplSetupInput()
    {
        int slots = Av1TplModelConstants.ReferenceFrameSlotCount;
        this.Group = new Av1TplGroup();
        this.Lookahead = new Av1EncoderFrame<TSample>[Av1TplModelConstants.MaximumLagBuffers + 1];
        this.FilteredFrames = new Av1EncoderFrame<TSample>[Av1TplModelConstants.MaximumFrameIndex];
        this.HasFilteredFrame = new bool[Av1TplModelConstants.MaximumFrameIndex];
        this.SlotFrames = new Av1EncoderFrame<TSample>[slots];
        this.SlotBufferIds = new int[slots];
        this.SlotDisplayOrderHints = new int[slots];
        this.PairDisplayOrders = new int[slots];
        this.PairPyramidLevels = new int[slots];
        this.MotionVectorContext = new Av1MotionVectorContext();
    }

    /// <summary>
    /// Gets or sets a value indicating whether the frame that starts the group is a key frame.
    /// </summary>
    public bool IsKeyFrame { get; set; }

    /// <summary>
    /// Gets or sets the display number of the frame that starts the group.
    /// </summary>
    public int FrameNumber { get; set; }

    /// <summary>
    /// Gets the golden group. The model writes the update type and quantizer of look-ahead extension entries.
    /// </summary>
    public Av1TplGroup Group { get; }

    /// <summary>
    /// Gets or sets the reference mapping of the group structure.
    /// </summary>
    public IAv1TplReferenceMapper? ReferenceMapper { get; set; }

    /// <summary>
    /// Gets the look-ahead source frames by offset from the frame that starts the group, with borders extended from the
    /// visible size.
    /// </summary>
    public Av1EncoderFrame<TSample>[] Lookahead { get; }

    /// <summary>
    /// Gets or sets the number of frames available in <see cref="Lookahead"/>.
    /// </summary>
    public int LookaheadCount { get; set; }

    /// <summary>
    /// Gets the temporally filtered source of each group entry that has one, with borders extended.
    /// </summary>
    public Av1EncoderFrame<TSample>[] FilteredFrames { get; }

    /// <summary>
    /// Gets a value for each group entry indicating whether <see cref="FilteredFrames"/> holds its filtered source.
    /// </summary>
    public bool[] HasFilteredFrame { get; }

    /// <summary>
    /// Gets the reconstruction held by each reference slot. The model ignores it for a key frame.
    /// </summary>
    public Av1EncoderFrame<TSample>[] SlotFrames { get; }

    /// <summary>
    /// Gets the identity of the buffer in each slot. Slots that hold the same buffer have the same identity.
    /// </summary>
    public int[] SlotBufferIds { get; }

    /// <summary>
    /// Gets the display order of the frame in each slot.
    /// </summary>
    public int[] SlotDisplayOrderHints { get; }

    /// <summary>
    /// Gets the display order of the frame in each slot, or -1 for an empty or repeated slot.
    /// </summary>
    public int[] PairDisplayOrders { get; }

    /// <summary>
    /// Gets the pyramid level of the frame in each slot, or -1.
    /// </summary>
    public int[] PairPyramidLevels { get; }

    /// <summary>
    /// Gets or sets the configured look-ahead depth.
    /// </summary>
    public int LagInFrames { get; set; }

    /// <summary>
    /// Gets or sets the number of frames to the next key frame.
    /// </summary>
    public int FramesToKey { get; set; }

    /// <summary>
    /// Gets or sets the golden interval of the group.
    /// </summary>
    public int BaselineGoldenInterval { get; set; }

    /// <summary>
    /// Gets or sets the golden boost in force when the model runs.
    /// </summary>
    public int GoldenBoost { get; set; }

    /// <summary>
    /// Gets or sets the lowest quantizer index of the rate control.
    /// </summary>
    public int BestQuality { get; set; }

    /// <summary>
    /// Gets or sets the highest quantizer index of the rate control.
    /// </summary>
    public int WorstQuality { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the rate control is in the constant-quality or the variable-bitrate mode.
    /// In these modes the model lowers the leaf quantizer.
    /// </summary>
    public bool AdjustLeafQuantizer { get; set; } = true;

    /// <summary>
    /// Gets or sets the model speed features.
    /// </summary>
    public Av1TplSpeedFeatures SpeedFeatures { get; set; }

    /// <summary>
    /// Gets or sets the frame motion search features that the model reuses: mesh patterns and thresholds, downsampled
    /// absolute differences, and the fractional search method with its iterations and taps. The model runs before the
    /// frame sets its own speed features. Thus these are the features of the frame coded before it. The first key frame
    /// is the exception, because its features are set first. If the first-pass statistics classified that earlier frame
    /// as graphics or animation, the exhaustive search threshold drops to its screen-content value. This changes the
    /// vectors of the eight-point search at speeds 0 and 1.
    /// </summary>
    public Av1MotionSearchSettings MotionSettings { get; set; }

    /// <summary>
    /// Gets the entropy context whose motion vector distributions price the model search. For a key frame the model
    /// resets it to the defaults.
    /// </summary>
    public Av1MotionVectorContext MotionVectorContext { get; }

    /// <summary>
    /// Gets or sets a value indicating whether eighth-sample vectors are allowed, as the most recently coded inter frame
    /// left the flag.
    /// </summary>
    public bool AllowHighPrecisionMotionVector { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether integer vectors are forced, as the previous frame left the flag.
    /// </summary>
    public bool ForceIntegerMotionVector { get; set; }

    /// <summary>
    /// Gets or sets the superblock quantizer delta that the quantizers of the model add to its quantizer. It is the delta
    /// of the last superblock when the previous coded frame codes delta quantizers, and zero otherwise.
    /// </summary>
    public int QuantizerDeltaQIndex { get; set; }

    /// <summary>
    /// Gets or sets the mode-information row that ends the first tile of the frame.
    /// </summary>
    public int TileModeInfoRowEnd { get; set; }

    /// <summary>
    /// Gets or sets the mode-information column that ends the first tile of the frame.
    /// </summary>
    public int TileModeInfoColumnEnd { get; set; }

    /// <summary>
    /// Gets or sets the tune metric.
    /// </summary>
    public Av1Tuning Tuning { get; set; }

    /// <summary>
    /// Gets or sets the encoder sharpness. At 3 it keeps the motion search near the frame.
    /// </summary>
    public int Sharpness { get; set; }

    /// <summary>
    /// Gets or sets the sharpness that the encoder last built the quantizer tables with. It sets the quantizer rounding.
    /// </summary>
    public int QuantizerSharpness { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the encoder consumes first-pass statistics. If it does, the rate multiplier
    /// gets the layer and boost adjustments. One-pass good quality with look-ahead consumes them.
    /// </summary>
    public bool IsStatConsumptionStage { get; set; } = true;

    /// <summary>
    /// Gets or sets the fixed quantizer offset mode.
    /// </summary>
    public int UseFixedQpOffsets { get; set; }

    /// <summary>
    /// Gets or sets the superblock size of the sequence.
    /// </summary>
    public Av1BlockSize SuperblockSize { get; set; } = Av1BlockSize.Block128x128;

    /// <summary>
    /// Gets or sets a value indicating whether the sequence enables the intra edge filter.
    /// </summary>
    public bool EnableIntraEdgeFilter { get; set; } = true;
}
