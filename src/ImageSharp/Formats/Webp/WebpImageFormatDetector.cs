// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;

namespace SixLabors.ImageSharp.Formats.Webp;

/// <summary>
/// Detects Webp file headers.
/// </summary>
public sealed class WebpImageFormatDetector : IImageFormatDetector
{
    /// <inheritdoc />
    public int HeaderSize => RiffOrListChunkHeader.HeaderSize;

    /// <inheritdoc />
    public bool TryDetectFormat(ReadOnlySpan<byte> header, [NotNullWhen(true)] out IImageFormat? format)
    {
        format = this.IsSupportedFileFormat(header) ? WebpFormat.Instance : null;
        return format != null;
    }

    private bool IsSupportedFileFormat(ReadOnlySpan<byte> header)
        => header.Length >= this.HeaderSize && RiffOrListChunkHeader.Parse(header) is
        {
            IsRiff: true,
            FormType: WebpConstants.WebpFourCc
        };
}
