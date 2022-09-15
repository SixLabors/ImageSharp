// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Bmp;

/// <summary>
/// Configuration options for decoding Windows Bitmap images.
/// </summary>
public sealed class BmpDecoderOptions : ISpecializedDecoderOptions
{
    /// <inheritdoc/>
    public DecoderOptions GeneralOptions { get; set; } = new();

    /// <summary>
    /// Gets or sets the value indicating how to deal with skipped pixels,
    /// which can occur during decoding run length encoded bitmaps.
    /// </summary>
    public RleSkippedPixelHandling RleSkippedPixelHandling { get; set; }
}
