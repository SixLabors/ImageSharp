// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Contains the AV1 deblocking-loop-filter parameters for a frame.
/// </summary>
internal sealed class ObuLoopFilterParameters
{
    /// <summary>
    /// Stores the horizontal and vertical luma filter levels.
    /// </summary>
    private InlineArray4<int> filterLevel;

    /// <summary>
    /// Stores the fixed reference-frame delta table.
    /// </summary>
    private InlineArray8<int> referenceDeltas;

    /// <summary>
    /// Stores the fixed prediction-mode delta table.
    /// </summary>
    private InlineArray4<int> modeDeltas;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObuLoopFilterParameters"/> class with the AV1 default reference and mode deltas.
    /// </summary>
    public ObuLoopFilterParameters()
    {
        // AV1 indexes this table from Intra through Alternate. Golden is -1; Backward remains 0.
        this.referenceDeltas[0] = 1;
        this.referenceDeltas[4] = -1;
        this.referenceDeltas[6] = -1;
        this.referenceDeltas[7] = -1;
    }

    /// <summary>
    /// Gets the horizontal and vertical luma filter levels.
    /// </summary>
    public Span<int> FilterLevel => this.filterLevel[..2];

    /// <summary>
    /// Gets or sets the U-plane filter level.
    /// </summary>
    public int FilterLevelU { get; set; }

    /// <summary>
    /// Gets or sets the V-plane filter level.
    /// </summary>
    public int FilterLevelV { get; set; }

    /// <summary>
    /// Gets or sets the filter sharpness level.
    /// </summary>
    public int SharpnessLevel { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether reference-frame and mode deltas are enabled.
    /// </summary>
    public bool ReferenceDeltaModeEnabled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether reference-frame and mode deltas are updated by this frame.
    /// </summary>
    public bool ReferenceDeltaModeUpdate { get; set; }

    /// <summary>
    /// Gets the filter-level deltas for the AV1 reference-frame categories.
    /// </summary>
    public Span<int> ReferenceDeltas => this.referenceDeltas;

    /// <summary>
    /// Gets the filter-level deltas for the AV1 prediction modes.
    /// </summary>
    public Span<int> ModeDeltas => this.modeDeltas[..2];
}
