// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Contains the availability and enabled state of AV1 skip mode for a frame.
/// </summary>
internal class ObuSkipModeParameters
{
    /// <summary>
    /// Gets or sets a value indicating whether the frame is permitted to use skip mode.
    /// </summary>
    public bool SkipModeAllowed { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether skip mode is enabled for the frame.
    /// </summary>
    public bool SkipModeFlag { get; internal set; }
}
