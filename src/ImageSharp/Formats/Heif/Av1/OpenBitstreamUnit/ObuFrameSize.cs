// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.OpenBitstreamUnit;

/// <summary>
/// Contains the coded, upscaled, and rendered dimensions of an AV1 frame.
/// </summary>
internal class ObuFrameSize
{
    /// <summary>
    /// Gets or sets the coded frame width.
    /// </summary>
    public int FrameWidth { get; set; }

    /// <summary>
    /// Gets or sets the coded frame height.
    /// </summary>
    public int FrameHeight { get; set; }

    /// <summary>
    /// Gets or sets the denominator used by AV1 super-resolution scaling.
    /// </summary>
    public int SuperResolutionDenominator { get; set; }

    /// <summary>
    /// Gets or sets the frame width after super-resolution upscaling.
    /// </summary>
    public int SuperResolutionUpscaledWidth { get; set; }

    /// <summary>
    /// Gets or sets the intended display width.
    /// </summary>
    public int RenderWidth { get; set; }

    /// <summary>
    /// Gets or sets the intended display height.
    /// </summary>
    public int RenderHeight { get; set; }
}
