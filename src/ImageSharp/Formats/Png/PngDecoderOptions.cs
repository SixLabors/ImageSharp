// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Png;

/// <summary>
/// Configuration options for decoding png images.
/// </summary>
public sealed class PngDecoderOptions : ISpecializedDecoderOptions
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PngDecoderOptions"/> class with the specified general options.
    /// </summary>
    /// <param name="generalOptions">The general options used to decode and identify images.</param>
    /// <exception cref="ArgumentNullException">The general options are null.</exception>
    public PngDecoderOptions(DecoderOptions generalOptions)
    {
        Guard.NotNull(generalOptions, nameof(generalOptions));

        this.GeneralOptions = generalOptions;
    }

    /// <inheritdoc/>
    public DecoderOptions GeneralOptions { get; }

    /// <summary>
    /// Gets the maximum memory in bytes that a zTXt, sPLT, iTXt, iCCP, or unknown chunk can occupy when decompressed.
    /// Defaults to 8MB
    /// </summary>
    public int MaxUncompressedAncillaryChunkSizeBytes { get; init; } = 8 * 1024 * 1024; // 8MB
}
