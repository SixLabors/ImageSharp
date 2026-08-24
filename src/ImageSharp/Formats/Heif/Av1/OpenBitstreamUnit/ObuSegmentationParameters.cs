// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Contains the AV1 segmentation state and per-segment feature values for a frame.
/// </summary>
internal class ObuSegmentationParameters
{
    /// <summary>
    /// Gets or sets the effective quantization-matrix level for each plane and segment.
    /// </summary>
    public int[][] QMLevel { get; internal set; } = new int[3][];

    /// <summary>
    /// Gets or sets the enabled state of every feature for every segment.
    /// </summary>
    public bool[,] FeatureEnabled { get; internal set; } = new bool[Av1Constants.MaxSegmentCount, Av1Constants.SegmentationLevelMax];

    /// <summary>
    /// Gets or sets a value indicating whether segmentation is enabled for the frame.
    /// </summary>
    public bool Enabled { get; internal set; }

    /// <summary>
    /// Gets or sets the value of every feature for every segment.
    /// </summary>
    public int[,] FeatureData { get; internal set; } = new int[Av1Constants.MaxSegmentCount, Av1Constants.SegmentationLevelMax];

    /// <summary>
    /// Gets or sets a value indicating whether segment identifiers are decoded before skip-mode decisions.
    /// </summary>
    public bool SegmentIdPrecedesSkip { get; internal set; }

    /// <summary>
    /// Gets or sets the greatest segment identifier that has at least one active feature.
    /// </summary>
    public int LastActiveSegmentId { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether the segmentation map is updated while decoding the frame.
    /// </summary>
    public int SegmentationUpdateMap { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether segmentation-map updates are coded relative to the existing map.
    /// </summary>
    public int SegmentationTemporalUpdate { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether the frame supplies new per-segment feature data.
    /// </summary>
    public int SegmentationUpdateData { get; internal set; }

    /// <summary>
    /// Determines whether a feature is active for a segment.
    /// </summary>
    /// <param name="segmentId">The segment identifier.</param>
    /// <param name="feature">The feature to inspect.</param>
    /// <returns><see langword="true"/> when the feature is active; otherwise, <see langword="false"/>.</returns>
    internal bool IsFeatureActive(int segmentId, ObuSegmentationLevelFeature feature)
        => this.FeatureEnabled[segmentId, (int)feature];
}
