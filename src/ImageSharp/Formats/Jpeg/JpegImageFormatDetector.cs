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
    public int HeaderSize => 2;

    /// <inheritdoc/>
    public bool TryDetectFormat(ReadOnlySpan<byte> header, [NotNullWhen(true)] out IImageFormat? format)
    {
        format = this.IsSupportedFileFormat(header) ? JpegFormat.Instance : null;
        return format != null;
    }

    private bool IsSupportedFileFormat(ReadOnlySpan<byte> header)
        => header.Length >= this.HeaderSize && IsJpeg(header);

    /// <summary>
    /// Returns a value indicating whether the given bytes identify Jpeg data.
    /// </summary>
    /// <param name="header">The bytes representing the file header.</param>
    /// <returns>The <see cref="bool"/></returns>
    private static bool IsJpeg(ReadOnlySpan<byte> header) =>
        header[0] == 0xFF && // 255
        header[1] == 0xD8; // 216
}
