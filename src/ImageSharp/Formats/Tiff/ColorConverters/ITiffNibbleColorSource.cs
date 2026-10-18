// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Tiff.ColorConverters;

/// <summary>
/// Reads a byte-padded row of regularly packed four-bit color components.
/// </summary>
internal interface ITiffNibbleColorSource : ITiffComponentSource<float>
{
    /// <summary>
    /// Gets the number of stored bits per pixel.
    /// </summary>
    int BitsPerPixel { get; }
}
