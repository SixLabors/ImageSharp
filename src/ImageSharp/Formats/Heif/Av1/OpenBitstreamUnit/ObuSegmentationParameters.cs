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

    internal bool IsFeatureActive(int segmentId, ObuSegmentationLevelFeature feature)
        => this.FeatureEnabled[segmentId, (int)feature];
}
