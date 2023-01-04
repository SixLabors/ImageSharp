// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;

namespace SixLabors.ImageSharp.Formats.Jpeg;

/// <summary>
/// Detects Jpeg file headers
/// </summary>
public sealed class JpegImageFormatDetector : IImageFormatDetector
{
    /// <inheritdoc/>
    public int HeaderSize => 11;

    /// <inheritdoc/>
    public bool TryDetectFormat(ReadOnlySpan<byte> header, [NotNullWhen(true)] out IImageFormat? format)
    {
        format = this.IsSupportedFileFormat(header) ? JpegFormat.Instance : null;
        return format != null;
    }

    private bool IsSupportedFileFormat(ReadOnlySpan<byte> header)
        => header.Length >= this.HeaderSize
        && (IsJfif(header) || IsExif(header) || IsJpeg(header));

    /// <summary>
    /// Returns a value indicating whether the given bytes identify Jfif data.
    /// </summary>
    /// <param name="header">The bytes representing the file header.</param>
    /// <returns>The <see cref="bool"/></returns>
    private static bool IsJfif(ReadOnlySpan<byte> header) =>
        header[6] == 0x4A && // J
        header[7] == 0x46 && // F
        header[8] == 0x49 && // I
        header[9] == 0x46 && // F
        header[10] == 0x00;

    /// <summary>
    /// Returns a value indicating whether the given bytes identify EXIF data.
    /// </summary>
    /// <param name="header">The bytes representing the file header.</param>
    /// <returns>The <see cref="bool"/></returns>
    private static bool IsExif(ReadOnlySpan<byte> header) =>
        header[6] == 0x45 && // E
        header[7] == 0x78 && // X
        header[8] == 0x69 && // I
        header[9] == 0x66 && // F
        header[10] == 0x00;

    /// <summary>
    /// Returns a value indicating whether the given bytes identify Jpeg data.
    /// This is a last chance resort for jpegs that contain ICC information.
    /// </summary>
    /// <param name="header">The bytes representing the file header.</param>
    /// <returns>The <see cref="bool"/></returns>
    private static bool IsJpeg(ReadOnlySpan<byte> header) =>
        header[0] == 0xFF && // 255
        header[1] == 0xD8; // 216
}
