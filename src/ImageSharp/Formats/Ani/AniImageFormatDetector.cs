// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.Formats.Webp;

namespace SixLabors.ImageSharp.Formats.Ani;

/// <summary>
/// Detects ico file headers.
/// </summary>
public class AniImageFormatDetector : IImageFormatDetector
{
    /// <inheritdoc/>
    public int HeaderSize => RiffOrListChunkHeader.HeaderSize;

    /// <inheritdoc/>
    public bool TryDetectFormat(ReadOnlySpan<byte> header, [NotNullWhen(true)] out IImageFormat? format)
    {
        format = this.IsSupportedFileFormat(header) ? AniFormat.Instance : null;
        return format is not null;
    }

    private bool IsSupportedFileFormat(ReadOnlySpan<byte> header)
        => header.Length >= this.HeaderSize && RiffOrListChunkHeader.Parse(header) is
        {
            IsRiff: true,
            FormType: AniConstants.AniFourCc
        };
}
