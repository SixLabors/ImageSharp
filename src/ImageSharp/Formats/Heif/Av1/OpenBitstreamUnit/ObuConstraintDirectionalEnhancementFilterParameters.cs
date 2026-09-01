// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Contains the constrained directional enhancement filter parameters for an AV1 frame.
/// </summary>
internal sealed class ObuConstraintDirectionalEnhancementFilterParameters
{
    /// <summary>
    /// Stores the fixed sixteen luma filter strengths without a per-frame array allocation.
    /// </summary>
    private InlineArray16<int> yStrength;

    /// <summary>
    /// Stores the fixed sixteen chroma filter strengths without a per-frame array allocation.
    /// </summary>
    private InlineArray16<int> uvStrength;

    /// <summary>
    /// Gets or sets the number of bits used to select a filter-strength entry.
    /// </summary>
    public int BitCount { get; set; }

    /// <summary>
    /// Gets or sets the filter damping value.
    /// </summary>
    public int Damping { get; set; } = 3;

    /// <summary>
    /// Gets the primary and secondary luma strengths for each filter entry.
    /// </summary>
    public Span<int> YStrength => this.yStrength;

    /// <summary>
    /// Gets the primary and secondary chroma strengths for each filter entry.
    /// </summary>
    public Span<int> UvStrength => this.uvStrength;
}
