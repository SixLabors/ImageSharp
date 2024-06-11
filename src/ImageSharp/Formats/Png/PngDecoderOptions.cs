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

    /// <summary>
    /// Gets the maximum memory in bytes that a zTXt, sPLT, iTXt, iCCP, or unknown chunk can occupy when decompressed.
    /// Defaults to 8MB
    /// </summary>
    public int MaxUncompressedAncillaryChunkSizeBytes { get; init; } = 8 * 1024 * 1024; // 8MB
}
