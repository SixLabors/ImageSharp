// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jpeg;

/// <summary>
/// Configuration options for decoding Jpeg images.
/// </summary>
public sealed class JpegDecoderOptions : ISpecializedDecoderOptions
{
    /// <inheritdoc/>
    public DecoderOptions GeneralOptions { get; init; } = DecoderOptions.Default;

    /// <summary>
    /// Gets the resize mode.
    /// </summary>
    public JpegDecoderResizeMode ResizeMode { get; init; }
}
