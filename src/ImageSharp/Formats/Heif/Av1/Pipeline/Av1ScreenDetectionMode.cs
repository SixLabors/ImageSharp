// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// The screen content detection mode.
/// </summary>
internal enum Av1ScreenDetectionMode
{
    /// <summary>
    /// The standard detection.
    /// </summary>
    Standard,

    /// <summary>
    /// The detection that also recognizes anti-aliased text and graphics.
    /// </summary>
    AntialiasingAware,
}
