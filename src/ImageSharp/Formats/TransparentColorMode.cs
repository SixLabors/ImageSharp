// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Specifies how transparent pixels should be handled during encoding and quantization.
/// </summary>
public enum TransparentColorMode
{
    /// <summary>
    /// Retains the original color values of transparent pixels.
    /// </summary>
    Preserve = 0,

    /// <summary>
    /// Converts transparent pixels with non-zero color components
    /// to fully transparent pixels (all components set to zero),
    /// which may improve compression.
    /// </summary>
    Clear = 1
}
