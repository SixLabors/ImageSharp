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
    internal int FrameWidth { get; set; }

    /// <summary>
    /// Gets or sets the coded frame height.
    /// </summary>
    internal int FrameHeight { get; set; }

    /// <summary>
    /// Gets or sets the denominator used by AV1 super-resolution scaling.
    /// </summary>
    internal int SuperResolutionDenominator { get; set; }

    /// <summary>
    /// Gets or sets the frame width after super-resolution upscaling.
    /// </summary>
    internal int SuperResolutionUpscaledWidth { get; set; }

    /// <summary>
    /// Gets or sets the intended display width.
    /// </summary>
    internal int RenderWidth { get; set; }

    /// <summary>
    /// Gets or sets the intended display height.
    /// </summary>
    internal int RenderHeight { get; set; }
}
