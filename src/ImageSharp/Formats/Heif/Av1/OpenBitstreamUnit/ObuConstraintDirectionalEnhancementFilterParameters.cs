// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Contains the constrained directional enhancement filter parameters for an AV1 frame.
/// </summary>
internal class ObuConstraintDirectionalEnhancementFilterParameters
{
    /// <summary>
    /// Gets or sets the number of bits used to select a filter-strength entry.
    /// </summary>
    public int BitCount { get; internal set; }

    /// <summary>
    /// Gets or sets the filter damping value.
    /// </summary>
    public int Damping { get; internal set; } = 3;

    /// <summary>
    /// Gets or sets the primary and secondary luma strengths for each filter entry.
    /// </summary>
    public int[] YStrength { get; set; } = new int[16];

    /// <summary>
    /// Gets or sets the primary and secondary chroma strengths for each filter entry.
    /// </summary>
    public int[] UvStrength { get; set; } = new int[16];
}
