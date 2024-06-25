// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

internal class ObuSegmentationParameters
{
    public int[][] QMLevel { get; internal set; } = new int[3][];

    public bool[,] FeatureEnabled { get; internal set; } = new bool[Av1Constants.MaxSegmentCount, Av1Constants.SegmentationLevelMax];

    public bool Enabled { get; internal set; }

    public int[,] FeatureData { get; internal set; } = new int[Av1Constants.MaxSegmentCount, Av1Constants.SegmentationLevelMax];

    public bool SegmentIdPrecedesSkip { get; internal set; }

    public int LastActiveSegmentId { get; internal set; }

    /// <summary>
    /// Gets or sets the SegmentationUpdateMap. A value of 1 indicates that the segmentation map are updated during the decoding of this
    /// frame. SegmentationUpdateMap equal to 0 means that the segmentation map from the previous frame is used.
    /// </summary>
    public int SegmentationUpdateMap { get; internal set; }

    /// <summary>
    /// Gets or sets the SegmentationTemporalUpdate. A value of 1 indicates that the updates to the segmentation map are coded relative to the
    /// existing segmentation map. SegmentationTemporalUpdate equal to 0 indicates that the new segmentation map is coded
    /// without reference to the existing segmentation map.
    /// </summary>
    public int SegmentationTemporalUpdate { get; internal set; }

    /// <summary>
    /// Gets or sets SegmentationUpdateData. A value of 1 indicates that new parameters are about to be specified for each segment.
    /// SegmentationUpdateData equal to 0 indicates that the segmentation parameters should keep their existing values.
    /// </summary>
    public int SegmentationUpdateData { get; internal set; }

    internal bool IsFeatureActive(int segmentId, ObuSegmentationLevelFeature feature)
        => this.FeatureEnabled[segmentId, (int)feature];
}
