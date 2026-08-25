// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Describes the relative horizontal and vertical spacing of pixels in a HEIF image.
/// </summary>
/// <param name="horizontalSpacing">The relative horizontal pixel spacing.</param>
/// <param name="verticalSpacing">The relative vertical pixel spacing.</param>
internal sealed class HeifPixelAspectRatio(uint horizontalSpacing, uint verticalSpacing)
{
    /// <summary>
    /// Gets the relative horizontal pixel spacing.
    /// </summary>
    public uint HorizontalSpacing { get; } = horizontalSpacing;

    /// <summary>
    /// Gets the relative vertical pixel spacing.
    /// </summary>
    public uint VerticalSpacing { get; } = verticalSpacing;
}
