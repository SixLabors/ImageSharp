// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal class ObuSequenceHeader
{
    public bool EnableFilterIntra { get; set; }

    public bool EnableCdef { get; set; }

    public bool IsStillPicture { get; set; }

    public bool IsReducedStillPictureHeader { get; set; }

    public ObuSequenceProfile SequenceProfile { get; set; }

    public ObuOperatingPoint[] OperatingPoint { get; set; } = new ObuOperatingPoint[1];

    public bool InitialDisplayDelayPresentFlag { get; set; }

    public bool DecoderModelInfoPresentFlag { get; set; }

    public object? TimingInfo { get; set; }

    public bool IsFrameIdNumbersPresent { get; set; }

    public int FrameWidthBits { get; set; }

    public int FrameHeightBits { get; set; }

    public int MaxFrameWidth { get; set; }

    public int MaxFrameHeight { get; set; }

    public bool Use128x128SuperBlock { get; set; }

    public Av1BlockSize SuperBlockSize { get; set; }

    public int ModeInfoSize { get; set; }

    public int SuperBlockSizeLog2 { get; set; }

    public int FilterIntraLevel { get; set; }

    public bool EnableIntraEdgeFilter { get; set; }

    public ObuOrderHintInfo OrderHintInfo { get; set; } = new ObuOrderHintInfo();

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
}
