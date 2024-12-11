// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Acts as a base encoder for all formats that are aware of and can handle alpha transparency.
/// </summary>
public abstract class AlphaAwareImageEncoder : ImageEncoder
{
    /// <summary>
    /// Gets or initializes the mode that determines how transparent pixels are handled during encoding.
    /// </summary>
    public TransparentColorMode TransparentColorMode { get; init; }
}
