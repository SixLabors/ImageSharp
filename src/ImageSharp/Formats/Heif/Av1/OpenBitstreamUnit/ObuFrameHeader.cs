// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal class ObuFrameHeader
{
    public bool ForceIntegerMotionVector { get; set; }

    public bool AllowIntraBlockCopy { get; set; }

    public bool UseReferenceFrameMotionVectors { get; set; }

    public bool AllowHighPrecisionMotionVector { get; set; }

    public ObuTileGroupHeader TilesInfo { get; set; } = new ObuTileGroupHeader();

    public bool CodedLossless { get; set; }

    public bool[] LosslessArray { get; set; } = new bool[Av1Constants.MaxSegmentCount];

    public ObuQuantizationParameters QuantizationParameters { get; set; } = new ObuQuantizationParameters();

    public ObuSegmentationParameters SegmentationParameters { get; set; } = new ObuSegmentationParameters();

    public bool AllLossless { get; set; }

    public bool AllowWarpedMotion { get; set; }

    public ObuReferenceMode ReferenceMode { get; set; }

    public ObuFilmGrainParameters FilmGrainParameters { get; set; } = new ObuFilmGrainParameters();

    public bool UseReducedTransformSet { get; set; }

    public ObuLoopFilterParameters LoopFilterParameters { get; set; } = new ObuLoopFilterParameters();

    public ObuLoopRestorationParameters LoopRestorationParameters { get; set; } = new ObuLoopRestorationParameters();

    public ObuConstraintDirectionalEnhancementFilterParameters CdefParameters { get; set; } = new ObuConstraintDirectionalEnhancementFilterParameters();

    public int ModeInfoStride { get; set; }

    public bool DisableFrameEndUpdateCdf { get; set; }

    public ObuSkipModeParameters SkipModeParameters { get; set; } = new ObuSkipModeParameters();

    public Av1TransformMode TransformMode { get; set; }

    public ObuDeltaParameters DeltaLoopFilterParameters { get; set; } = new ObuDeltaParameters();

    public ObuDeltaParameters DeltaQParameters { get; set; } = new ObuDeltaParameters();

    public bool IsIntra => this.FrameType is ObuFrameType.IntraOnlyFrame or ObuFrameType.KeyFrame;

    internal ObuFrameSize FrameSize { get; set; } = new ObuFrameSize();

    internal int ModeInfoColumnCount { get; set; }

    internal int ModeInfoRowCount { get; set; }

    internal bool ShowExistingFrame { get; set; }

    internal ObuFrameType FrameType { get; set; }

    internal bool[] ReferenceValid { get; set; } = new bool[Av1Constants.ReferenceFrameCount];

    internal bool[] ReferenceOrderHint { get; set; } = new bool[Av1Constants.ReferenceFrameCount];

    internal bool ShowFrame { get; set; }

    internal bool ShowableFrame { get; set; }

    internal uint FrameToShowMapIdx { get; set; }

    internal uint DisplayFrameId { get; set; }

    internal bool ErrorResilientMode { get; set; }

    internal bool AllowScreenContentTools { get; set; }

    internal bool DisableCdfUpdate { get; set; }

    internal uint CurrentFrameId { get; set; }

    internal uint[] ReferenceFrameIndex { get; set; } = new uint[Av1Constants.ReferenceFrameCount];

    internal uint OrderHint { get; set; }

    internal uint PrimaryReferenceFrame { get; set; } = Av1Constants.PrimaryReferenceFrameNone;

    internal uint RefreshFrameFlags { get; set; }

    // 5.9.31. Temporal point info syntax
    internal uint FramePresentationTime { get; set; }
}
