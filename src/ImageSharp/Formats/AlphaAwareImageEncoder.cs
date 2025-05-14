// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Processing.Processors.Quantization;

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Acts as a base encoder for all formats that are aware of and can handle alpha transparency.
/// </summary>
public abstract class AlphaAwareImageEncoder : ImageEncoder
{
    /// <summary>
    /// Gets or initializes the mode that determines how transparent pixels are handled during encoding.
    /// This overrides any other settings that may affect the encoding of transparent pixels
    /// including those passed via <see cref="QuantizerOptions"/>.
    /// </summary>
    public TransparentColorMode TransparentColorMode { get; init; }
}
