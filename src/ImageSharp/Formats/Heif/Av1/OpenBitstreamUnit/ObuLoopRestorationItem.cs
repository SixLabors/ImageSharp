// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Contains the loop-restoration type and unit size for one color plane.
/// </summary>
internal class ObuLoopRestorationItem
{
    /// <summary>
    /// Gets or sets the restoration-unit size, in samples.
    /// </summary>
    public int Size { get; set; }

    /// <summary>
    /// Gets or sets the restoration filter type.
    /// </summary>
    public ObuRestorationType Type { get; set; } = ObuRestorationType.None;
}
