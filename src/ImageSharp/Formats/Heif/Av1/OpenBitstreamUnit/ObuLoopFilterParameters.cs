// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Contains the AV1 deblocking-loop-filter parameters for a frame.
/// </summary>
internal class ObuLoopFilterParameters
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ObuLoopFilterParameters"/> class with the AV1 default reference and mode deltas.
    /// </summary>
    public ObuLoopFilterParameters()
    {
        // AV1 indexes this table from Intra through Alternate. Golden is -1; Backward remains 0.
        this.ReferenceDeltas = [1, 0, 0, 0, -1, 0, -1, -1];
        this.ModeDeltas = [0, 0];
    }

    /// <summary>
    /// Gets or sets the horizontal and vertical luma filter levels.
    /// </summary>
    public int[] FilterLevel { get; internal set; } = new int[2];

    /// <summary>
    /// Gets or sets the U-plane filter level.
    /// </summary>
    public int FilterLevelU { get; internal set; }

    /// <summary>
    /// Gets or sets the V-plane filter level.
    /// </summary>
    public int FilterLevelV { get; internal set; }

    /// <summary>
    /// Gets or sets the filter sharpness level.
    /// </summary>
    public int SharpnessLevel { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether reference-frame and mode deltas are enabled.
    /// </summary>
    public bool ReferenceDeltaModeEnabled { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether reference-frame and mode deltas are updated by this frame.
    /// </summary>
    public bool ReferenceDeltaModeUpdate { get; internal set; }

    /// <summary>
    /// Gets the filter-level deltas for the AV1 reference-frame categories.
    /// </summary>
    public int[] ReferenceDeltas { get; }

    /// <summary>
    /// Gets the filter-level deltas for the AV1 prediction modes.
    /// </summary>
    public int[] ModeDeltas { get; }
}
