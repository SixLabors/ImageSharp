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
        Span<int> referenceDeltas = this.referenceDeltas;
        for (int i = 0; i < Av1Constants.TotalReferencesPerFrame; i++)
        {
            referenceDeltas[i] = GetDefaultReferenceDelta(i);
        }
    }

    /// <summary>
    /// Gets the default reference deltas, indexed from Intra through Alternate.
    /// </summary>
    /// <remarks>
    /// Intra blocks filter one level stronger. Golden, Alternate-2 and Alternate filter one level weaker. The other references keep the frame
    /// level. The default mode deltas are zero.
    /// </remarks>
    private static ReadOnlySpan<sbyte> DefaultReferenceDeltas => [1, 0, 0, 0, -1, 0, -1, -1];

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

    /// <summary>
    /// Gets the default delta of one reference frame type.
    /// </summary>
    /// <param name="reference">The reference index, from Intra through Alternate.</param>
    /// <returns>The default delta.</returns>
    public static int GetDefaultReferenceDelta(int reference) => DefaultReferenceDeltas[reference];
}
