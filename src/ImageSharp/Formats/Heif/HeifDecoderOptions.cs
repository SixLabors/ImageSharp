// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Configuration options for decoding HEIF images.
/// </summary>
public sealed class HeifDecoderOptions : ISpecializedDecoderOptions
{
    /// <inheritdoc/>
    public DecoderOptions GeneralOptions { get; init; } = new();

    /// <summary>
    /// Gets the chroma upsampling mode. The default is <see cref="HeifChromaUpsampling.Auto"/>.
    /// </summary>
    public HeifChromaUpsampling ChromaUpsampling { get; init; }
}
