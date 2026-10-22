// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Configuration options for decoding HEIF images.
/// </summary>
public sealed class HeifDecoderOptions : ISpecializedDecoderOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HeifDecoderOptions"/> class with the specified general options.
    /// </summary>
    /// <param name="generalOptions">The general options used to decode and identify images.</param>
    /// <exception cref="ArgumentNullException">The general options are null.</exception>
    public HeifDecoderOptions(DecoderOptions generalOptions)
    {
        Guard.NotNull(generalOptions, nameof(generalOptions));

        this.GeneralOptions = generalOptions;
    }

    /// <inheritdoc/>
    public DecoderOptions GeneralOptions { get; }

    /// <summary>
    /// Gets the chroma upsampling mode. The default is <see cref="HeifChromaUpsampling.Auto"/>.
    /// </summary>
    public HeifChromaUpsampling ChromaUpsampling { get; init; }
}
