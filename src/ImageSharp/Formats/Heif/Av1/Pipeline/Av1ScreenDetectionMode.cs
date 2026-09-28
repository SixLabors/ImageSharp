// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Pipeline;

/// <summary>
/// The screen content detection mode. Reference: aom_screen_detection_mode.
/// </summary>
internal enum Av1ScreenDetectionMode
{
    /// <summary>
    /// The standard detection. Reference: AOM_SCREEN_DETECTION_STANDARD.
    /// </summary>
    Standard,

    /// <summary>
    /// The detection that also recognizes anti-aliased text and graphics. Reference:
    /// AOM_SCREEN_DETECTION_ANTIALIASING_AWARE.
    /// </summary>
    AntialiasingAware,
}
