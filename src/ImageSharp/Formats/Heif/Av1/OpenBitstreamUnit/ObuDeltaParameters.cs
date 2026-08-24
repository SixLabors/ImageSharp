// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Contains the delta-quantizer or delta-loop-filter signaling parameters for an AV1 frame.
/// </summary>
internal class ObuDeltaParameters
{
    /// <summary>
    /// Gets or sets a value indicating whether per-block delta values are present.
    /// </summary>
    public bool IsPresent { get; internal set; }

    /// <summary>
    /// Gets or sets the delta-value resolution.
    /// </summary>
    public int Resolution { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether separate loop-filter deltas are signaled for multiple filter targets.
    /// </summary>
    public bool IsMulti { get; internal set; }
}
