// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Bmp;

/// <summary>
/// Configuration options for decoding Windows Bitmap images.
/// </summary>
public sealed class BmpDecoderOptions : ISpecializedDecoderOptions
{
    /// <inheritdoc/>
    public DecoderOptions GeneralOptions { get; init; } = new();

    /// <summary>
    /// Gets the value indicating how to deal with skipped pixels,
    /// which can occur during decoding run length encoded bitmaps.
    /// </summary>
    public RleSkippedPixelHandling RleSkippedPixelHandling { get; init; }

    /// <summary>
    /// Gets a value indicating whether the additional AlphaMask is processed at decoding time.
    /// </summary>
    /// <remarks>
    /// It will be used at IcoDecoder.
    /// </remarks>
    internal bool ProcessedAlphaMask { get; init; }

    /// <summary>
    /// Gets a value indicating whether to skip loading the BMP file header.
    /// </summary>
    /// <remarks>
    /// It will be used at IcoDecoder.
    /// </remarks>
    internal bool SkipFileHeader { get; init; }

    /// <summary>
    /// Gets a value indicating whether the height is double of true height.
    /// </summary>
    /// <remarks>
    /// It will be used at IcoDecoder.
    /// </remarks>
    internal bool IsDoubleHeight { get; init; }
}
