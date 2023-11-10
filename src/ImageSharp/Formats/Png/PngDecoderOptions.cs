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
    /// If true, ADLER32 checksum in the IDAT chunk as well as the chunk CRCs will be ignored.
    /// Similar to PNG_CRC_QUIET_USE in libpng.
    /// </summary>
    public bool IgnoreCrcCheck { get; init; }
}
