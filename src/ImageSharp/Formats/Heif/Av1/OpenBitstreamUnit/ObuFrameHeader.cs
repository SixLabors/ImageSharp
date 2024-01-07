// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal class ObuFrameHeader
{
    public bool ForceIntegerMotionVector { get; set; }

    public bool AllowIntraBlockCopy { get; set; }

    public bool UseReferenceFrameMotionVectors { get; set; }

    public bool AllowHighPrecisionMotionVector { get; set; }

    public ObuTileInfo TilesInfo { get; internal set; } = new ObuTileInfo();

    public bool CodedLossless { get; internal set; }

    public bool[] LosslessArray { get; internal set; } = new bool[ObuConstants.MaxSegmentCount];

    public ObuQuantizationParameters QuantizationParameters { get; set; } = new ObuQuantizationParameters();

    public ObuSegmentationParameters SegmentationParameters { get; set; } = new ObuSegmentationParameters();

    public bool AllLossless { get; internal set; }

    public bool AllowWarpedMotion { get; internal set; }

    public ObuReferenceMode ReferenceMode { get; internal set; }

    public ObuFilmGrainParameters FilmGrainParameters { get; internal set; } = new ObuFilmGrainParameters();

    public bool ReducedTxSet { get; internal set; }

    public ObuLoopFilterParameters LoopFilterParameters { get; internal set; } = new ObuLoopFilterParameters();

    public ObuLoopRestorationParameters[] LoopRestorationParameters { get; internal set; } = new ObuLoopRestorationParameters[3];

    public ObuConstraintDirectionalEnhancementFilterParameters ConstraintDirectionalEnhancementFilterParameters { get; internal set; } = new ObuConstraintDirectionalEnhancementFilterParameters();

    public int ModeInfoStride { get; internal set; }

    public bool DisableFrameEndUpdateCdf { get; internal set; }

    internal ObuFrameSize FrameSize { get; set; } = new ObuFrameSize();

    internal int ModeInfoColumnCount { get; set; }

    internal int ModeInfoRowCount { get; set; }

    internal bool ShowExistingFrame { get; set; }

    internal ObuFrameType FrameType { get; set; }

    internal bool[] ReferenceValid { get; set; } = new bool[ObuConstants.ReferenceFrameCount];

    internal bool[] ReferenceOrderHint { get; set; } = new bool[ObuConstants.ReferenceFrameCount];

    internal bool ShowFrame { get; set; }

    internal bool ShowableFrame { get; set; }

    internal bool ErrorResilientMode { get; set; }

    internal bool AllowScreenContentTools { get; set; }

    internal bool DisableCdfUpdate { get; set; }

    internal bool ForeceIntegerMotionVector { get; set; }

    internal uint CurrentFrameId { get; set; }

    internal uint[] ReferenceFrameIndex { get; set; } = new uint[ObuConstants.ReferenceFrameCount];

    internal uint OrderHint { get; set; }

    internal uint PrimaryReferenceFrame { get; set; } = ObuConstants.PrimaryReferenceFrameNone;

    internal uint RefreshFrameFlags { get; set; }
}
