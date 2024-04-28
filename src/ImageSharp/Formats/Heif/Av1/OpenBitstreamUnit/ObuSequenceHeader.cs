// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal class ObuSequenceHeader
{
    internal bool EnableFilterIntra { get; set; }

    internal bool EnableCdef { get; set; }

    internal bool IsStillPicture { get; set; }

    internal bool IsReducedStillPictureHeader { get; set; }

    internal ObuSequenceProfile SequenceProfile { get; set; }

    internal ObuOperatingPoint[] OperatingPoint { get; set; } = new ObuOperatingPoint[1];

    internal bool InitialDisplayDelayPresentFlag { get; set; }

    internal bool DecoderModelInfoPresentFlag { get; set; }

    internal object? TimingInfo { get; set; }

    internal bool IsFrameIdNumbersPresent { get; set; }

    internal int FrameWidthBits { get; set; }

    internal int FrameHeightBits { get; set; }

    internal int MaxFrameWidth { get; set; }

    internal int MaxFrameHeight { get; set; }

    internal bool Use128x128SuperBlock { get; set; }

    internal Av1BlockSize SuperBlockSize { get; set; }

    internal int ModeInfoSize { get; set; }

    internal int SuperBlockSizeLog2 { get; set; }

    internal int FilterIntraLevel { get; set; }

    internal bool EnableIntraEdgeFilter { get; set; }

    internal ObuOrderHintInfo OrderHintInfo { get; set; } = new ObuOrderHintInfo();

    internal bool EnableInterIntraCompound { get; set; }

    internal bool EnableMaskedCompound { get; set; }

    internal bool EnableWarpedMotion { get; set; }

    internal bool EnableDualFilter { get; set; }

    internal int SequenceForceIntegerMotionVector { get; set; }

    internal int SequenceForceScreenContentTools { get; set; }

    internal bool EnableSuperResolution { get; set; }

    internal int CdefLevel { get; set; }

    internal bool EnableRestoration { get; set; }

    internal ObuColorConfig ColorConfig { get; set; } = new ObuColorConfig();

    internal bool AreFilmGrainingParametersPresent { get; set; }

    internal int FrameIdLength { get; set; }

    internal int DeltaFrameIdLength { get; set; }
}
