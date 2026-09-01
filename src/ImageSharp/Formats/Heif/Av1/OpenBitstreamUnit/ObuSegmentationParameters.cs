// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Contains the AV1 segmentation state and per-segment feature values for a frame.
/// </summary>
internal sealed class ObuSegmentationParameters
{
    /// <summary>
    /// Stores the feature-enable flags for each segment and feature without per-frame array allocations.
    /// </summary>
    private InlineArray8<InlineArray8<bool>> featureEnabled;

    /// <summary>
    /// Stores the feature values for each segment and feature without per-frame array allocations.
    /// </summary>
    private InlineArray8<InlineArray8<int>> featureData;

    /// <summary>
    /// Stores the effective quantization-matrix level for each plane and segment without jagged arrays.
    /// </summary>
    private InlineArray4<InlineArray8<int>> qmLevel;

    /// <summary>
    /// Gets the mutable effective quantization-matrix levels for the Y, U, and V planes.
    /// </summary>
    public Span<InlineArray8<int>> QMLevel => this.qmLevel[..Av1Constants.MaxPlanes];

    /// <summary>
    /// Gets or sets a value indicating whether segmentation is enabled for the frame.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether segment identifiers are decoded before skip-mode decisions.
    /// </summary>
    public bool SegmentIdPrecedesSkip { get; set; }

    /// <summary>
    /// Gets or sets the greatest segment identifier that has at least one active feature.
    /// </summary>
    public int LastActiveSegmentId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the segmentation map is updated while decoding the frame.
    /// </summary>
    public int SegmentationUpdateMap { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether segmentation-map updates are coded relative to the existing map.
    /// </summary>
    public int SegmentationTemporalUpdate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the frame supplies new per-segment feature data.
    /// </summary>
    public int SegmentationUpdateData { get; set; }

    /// <summary>
    /// Determines whether a feature is active for a segment.
    /// </summary>
    /// <param name="segmentId">The segment identifier.</param>
    /// <param name="feature">The feature to inspect.</param>
    /// <returns><see langword="true"/> when the feature is active; otherwise, <see langword="false"/>.</returns>
    public bool IsFeatureActive(int segmentId, ObuSegmentationLevelFeature feature)
        => this.featureEnabled[segmentId][(int)feature];

    /// <summary>
    /// Gets a feature value for a segment.
    /// </summary>
    /// <param name="segmentId">The segment identifier.</param>
    /// <param name="featureId">The feature identifier.</param>
    /// <returns>The stored feature value.</returns>
    public int GetFeatureData(int segmentId, int featureId) => this.featureData[segmentId][featureId];

    /// <summary>
    /// Sets whether a feature is active for a segment.
    /// </summary>
    /// <param name="segmentId">The segment identifier.</param>
    /// <param name="featureId">The feature identifier.</param>
    /// <param name="enabled">Whether the feature is active.</param>
    public void SetFeatureEnabled(int segmentId, int featureId, bool enabled)
        => this.featureEnabled[segmentId][featureId] = enabled;

    /// <summary>
    /// Sets a feature value for a segment.
    /// </summary>
    /// <param name="segmentId">The segment identifier.</param>
    /// <param name="featureId">The feature identifier.</param>
    /// <param name="value">The feature value.</param>
    public void SetFeatureData(int segmentId, int featureId, int value)
        => this.featureData[segmentId][featureId] = value;

    /// <summary>
    /// Replaces every feature enable flag and value with state from a primary reference frame.
    /// </summary>
    /// <param name="source">The primary-reference segmentation state.</param>
    public void CopyFeaturesFrom(ObuSegmentationParameters source)
    {
        // AV1 inherits feature data but not the current frame's enabled or update flags. Both dimensions are fixed by
        // the bitstream syntax, and copying values into this header prevents retained frames from sharing mutable state.
        for (int segment = 0; segment < Av1Constants.MaxSegmentCount; segment++)
        {
            for (int feature = 0; feature < Av1Constants.SegmentationLevelMax; feature++)
            {
                this.featureEnabled[segment][feature] = source.featureEnabled[segment][feature];
                this.featureData[segment][feature] = source.featureData[segment][feature];
            }
        }
    }
}
