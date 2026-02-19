// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal class ObuSequenceHeader
{
    private bool use128x128Superblock;

    public bool EnableFilterIntra { get; set; }

    public bool EnableCdef { get; set; }

    public bool IsStillPicture { get; set; }

    public bool IsReducedStillPictureHeader { get; set; }

    public ObuSequenceProfile SequenceProfile { get; set; }

    public ObuOperatingPoint[] OperatingPoint { get; set; } = new ObuOperatingPoint[1];

    public ObuDecoderModelInfo? DecoderModelInfo { get; set; }

    public bool InitialDisplayDelayPresentFlag { get; set; }

    public bool DecoderModelInfoPresentFlag { get; set; }

    public bool TimingInfoPresentFlag { get; set; }

    public ObuTimingInfo? TimingInfo { get; set; }

    public bool IsFrameIdNumbersPresent { get; set; }

    public int FrameWidthBits { get; set; }

    public int FrameHeightBits { get; set; }

    public int MaxFrameWidth { get; set; }

    public int MaxFrameHeight { get; set; }

    public bool Use128x128Superblock
    {
        get => this.use128x128Superblock;
        set
        {
            this.use128x128Superblock = value;
            this.SuperblockSize = value ? Av1BlockSize.Block128x128 : Av1BlockSize.Block64x64;
            this.SuperblockSizeLog2 = value ? 7 : 6;
            this.SuperblockModeInfoSize = 1 << (this.SuperblockSizeLog2 - Av1Constants.ModeInfoSizeLog2);
        }
    }

    public Av1BlockSize SuperblockSize { get; private set; }

    public int SuperblockModeInfoSize { get; private set; }

    public int SuperblockSizeLog2 { get; private set; }

    public int FilterIntraLevel { get; set; }

    public bool EnableIntraEdgeFilter { get; set; }

    public ObuOrderHintInfo OrderHintInfo { get; set; } = new ObuOrderHintInfo();

    public bool EnableOrderHint { get; set; }

    public bool EnableInterIntraCompound { get; set; }

    public bool EnableMaskedCompound { get; set; }

    public bool EnableWarpedMotion { get; set; }

    public bool EnableDualFilter { get; set; }

    public int ForceIntegerMotionVector { get; set; }

    public int ForceScreenContentTools { get; set; }

    public bool EnableSuperResolution { get; set; }

    public int CdefLevel { get; set; }

    public bool EnableRestoration { get; set; }

    public ObuColorConfig ColorConfig { get; set; } = new ObuColorConfig();

    public bool AreFilmGrainingParametersPresent { get; set; }

    public int FrameIdLength { get; set; }

    public int DeltaFrameIdLength { get; set; }

    public uint AdditionalFrameIdLength { get; set; }
}
