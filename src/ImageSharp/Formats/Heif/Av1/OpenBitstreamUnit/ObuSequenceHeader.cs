// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Stores AV1 sequence capabilities, dimensions, timing, and color configuration.
/// </summary>
internal sealed class ObuSequenceHeader
{
    /// <summary>
    /// Backs <see cref="Use128x128Superblock"/> while its dependent geometry is updated.
    /// </summary>
    private bool use128x128Superblock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObuSequenceHeader"/> class with the normative 64x64 superblock geometry.
    /// </summary>
    public ObuSequenceHeader() => this.Use128x128Superblock = false;

    /// <summary>
    /// Gets or sets a value indicating whether filter-intra prediction is enabled.
    /// </summary>
    public bool EnableFilterIntra { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether constrained directional enhancement filtering is enabled.
    /// </summary>
    public bool EnableCdef { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the sequence contains one still picture.
    /// </summary>
    public bool IsStillPicture { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the reduced still-picture header syntax is used.
    /// </summary>
    public bool IsReducedStillPictureHeader { get; set; }

    /// <summary>
    /// Gets or sets the sequence profile.
    /// </summary>
    public ObuSequenceProfile SequenceProfile { get; set; }

    /// <summary>
    /// Gets or sets the signaled operating points.
    /// </summary>
    public ObuOperatingPoint[] OperatingPoint { get; set; } = new ObuOperatingPoint[1];

    /// <summary>
    /// Gets or sets the decoder-buffer model information when present.
    /// </summary>
    public ObuDecoderModelInfo? DecoderModelInfo { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether operating points may carry initial display delays.
    /// </summary>
    public bool InitialDisplayDelayPresentFlag { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether decoder-model information is present.
    /// </summary>
    public bool DecoderModelInfoPresentFlag { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether sequence timing information is present.
    /// </summary>
    public bool TimingInfoPresentFlag { get; set; }

    /// <summary>
    /// Gets or sets the sequence timing information when present.
    /// </summary>
    public ObuTimingInfo? TimingInfo { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether frame identifiers are signaled.
    /// </summary>
    public bool IsFrameIdNumbersPresent { get; set; }

    /// <summary>
    /// Gets or sets the number of bits used to signal frame widths minus one.
    /// </summary>
    public int FrameWidthBits { get; set; }

    /// <summary>
    /// Gets or sets the number of bits used to signal frame heights minus one.
    /// </summary>
    public int FrameHeightBits { get; set; }

    /// <summary>
    /// Gets or sets the maximum decoded frame width in pixels.
    /// </summary>
    public int MaxFrameWidth { get; set; }

    /// <summary>
    /// Gets or sets the maximum decoded frame height in pixels.
    /// </summary>
    public int MaxFrameHeight { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether 128x128 superblocks are used.
    /// </summary>
    public bool Use128x128Superblock
    {
        get => this.use128x128Superblock;
        set
        {
            this.use128x128Superblock = value;

            // Superblock selection controls every downstream partition-grid unit, so update the derived geometry atomically.
            this.SuperblockSize = value ? Av1BlockSize.Block128x128 : Av1BlockSize.Block64x64;
            this.SuperblockSizeLog2 = value ? 7 : 6;
            this.SuperblockModeInfoSize = 1 << (this.SuperblockSizeLog2 - Av1Constants.ModeInfoSizeLog2);
        }
    }

    /// <summary>
    /// Gets the selected superblock dimensions.
    /// </summary>
    public Av1BlockSize SuperblockSize { get; private set; }

    /// <summary>
    /// Gets the superblock width and height in mode-information units.
    /// </summary>
    public int SuperblockModeInfoSize { get; private set; }

    /// <summary>
    /// Gets the base-two logarithm of the superblock size in pixels.
    /// </summary>
    public int SuperblockSizeLog2 { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether intra edge filtering is enabled.
    /// </summary>
    public bool EnableIntraEdgeFilter { get; set; }

    /// <summary>
    /// Gets or sets the order-hint capabilities.
    /// </summary>
    public ObuOrderHintInfo OrderHintInfo { get; set; } = new ObuOrderHintInfo();

    /// <summary>
    /// Gets or sets a value indicating whether order hints are enabled.
    /// </summary>
    public bool EnableOrderHint
    {
        get => this.OrderHintInfo.EnableOrderHint;
        set
        {
            // Order-hint availability is consumed through OrderHintInfo by frame parsing, so
            // keep the sequence-facing flag and dependent tool state synchronized.
            this.OrderHintInfo.EnableOrderHint = value;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether inter-intra compound prediction is enabled.
    /// </summary>
    public bool EnableInterIntraCompound { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether masked compound prediction is enabled.
    /// </summary>
    public bool EnableMaskedCompound { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether warped motion is enabled.
    /// </summary>
    public bool EnableWarpedMotion { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether independent horizontal and vertical interpolation filters are enabled.
    /// </summary>
    public bool EnableDualFilter { get; set; }

    /// <summary>
    /// Gets or sets the sequence-level integer-motion-vector selection.
    /// </summary>
    public int ForceIntegerMotionVector { get; set; }

    /// <summary>
    /// Gets or sets the sequence-level screen-content-tools selection.
    /// </summary>
    public int ForceScreenContentTools { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether frame super-resolution is enabled.
    /// </summary>
    public bool EnableSuperResolution { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether loop restoration is enabled.
    /// </summary>
    public bool EnableRestoration { get; set; }

    /// <summary>
    /// Gets or sets the decoded color configuration.
    /// </summary>
    public ObuColorConfig ColorConfig { get; set; } = new ObuColorConfig();

    /// <summary>
    /// Gets or sets a value indicating whether frame headers may carry film-grain parameters.
    /// </summary>
    public bool AreFilmGrainingParametersPresent { get; set; }

    /// <summary>
    /// Gets or sets the total number of bits in a frame identifier.
    /// </summary>
    public int FrameIdLength { get; set; }

    /// <summary>
    /// Gets or sets the number of bits in a delta frame identifier.
    /// </summary>
    public int DeltaFrameIdLength { get; set; }

    /// <summary>
    /// Gets or sets the additional frame-identifier bit count signaled by the sequence header.
    /// </summary>
    public uint AdditionalFrameIdLength { get; set; }

    /// <summary>
    /// Gets the decoder-buffer model information required by syntax whose presence flag is set.
    /// </summary>
    /// <returns>The decoder-buffer model information.</returns>
    public ObuDecoderModelInfo GetDecoderModelInfo() =>
        this.DecoderModelInfo
        ?? throw new InvalidOperationException("The AV1 sequence has no decoder-model information.");
}
