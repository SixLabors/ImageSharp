// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.Formats.Icon;

namespace SixLabors.ImageSharp.Formats.Ico;

/// <summary>
/// Detects ICO file headers.
/// </summary>
public sealed class IcoImageFormatDetector : IImageFormatDetector
{
    /// <inheritdoc/>
    public int HeaderSize => IconDir.Size + IconDirEntry.Size;

    /// <inheritdoc/>
    public bool TryDetectFormat(ReadOnlySpan<byte> header, [NotNullWhen(true)] out IImageFormat? format)
    {
        format = this.IsSupportedFileFormat(header) ? IcoFormat.Instance : null;
        return format is not null;
    }

    /// <summary>
    /// Determines whether the supplied header contains a valid ICO directory and first entry.
    /// </summary>
    /// <param name="header">The candidate file header.</param>
    /// <returns><see langword="true"/> when the header identifies ICO data.</returns>
    private bool IsSupportedFileFormat(ReadOnlySpan<byte> header)
    {
        if (header.Length < this.HeaderSize)
        {
            return false;
        }

        IconDir dir = IconDir.Parse(header);
        if (dir is not { Reserved: 0, Type: IconFileType.ICO } || dir.Count is 0)
        {
            return false;
        }

        IconDirEntry entry = IconDirEntry.Parse(header[IconDir.Size..]);

        // The first payload must begin after the complete directory, even when the caller supplied only the detection prefix.
        return entry is { Reserved: 0, Planes: 0 or 1, BitCount: 1 or 4 or 8 or 16 or 24 or 32 }
            && entry.BytesInRes is not 0
            && entry.ImageOffset >= IconDir.Size + (dir.Count * IconDirEntry.Size);
    }
}
