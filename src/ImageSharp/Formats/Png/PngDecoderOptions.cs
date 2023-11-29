// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Png;

/// <summary>
/// Configuration options for decoding png images.
/// </summary>
public sealed class PngDecoderOptions : ISpecializedDecoderOptions
{
    /// <inheritdoc/>
    public DecoderOptions GeneralOptions { get; init; } = new DecoderOptions();

    /// <summary>
    /// Gets a value indicating how to handle validation of any CRC (Cyclic Redundancy Check) data within the encoded PNG.
    /// </summary>
    public PngCrcChunkHandling PngCrcChunkHandling { get; init; } = PngCrcChunkHandling.IgnoreNonCritical;
}
