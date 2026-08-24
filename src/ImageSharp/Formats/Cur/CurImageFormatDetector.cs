// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using SixLabors.ImageSharp.Formats.Icon;

namespace SixLabors.ImageSharp.Formats.Cur;

/// <summary>
/// Detects CUR file headers.
/// </summary>
public sealed class CurImageFormatDetector : IImageFormatDetector
{
    /// <inheritdoc/>
    public int HeaderSize => IconDir.Size + IconDirEntry.Size;

    /// <inheritdoc/>
    public bool TryDetectFormat(ReadOnlySpan<byte> header, [NotNullWhen(true)] out IImageFormat? format)
    {
        format = this.IsSupportedFileFormat(header) ? CurFormat.Instance : null;
        return format is not null;
    }

    /// <summary>
    /// Determines whether the supplied header contains a valid CUR directory and first entry.
    /// </summary>
    /// <param name="header">The candidate file header.</param>
    /// <returns><see langword="true"/> when the header identifies CUR data.</returns>
    private bool IsSupportedFileFormat(ReadOnlySpan<byte> header)
    {
        if (header.Length < this.HeaderSize)
        {
            return false;
        }

        IconDir dir = IconDir.Parse(header);
        if (dir is not { Reserved: 0, Type: IconFileType.CUR } || dir.Count is 0)
        {
            return false;
        }

        IconDirEntry entry = IconDirEntry.Parse(header[IconDir.Size..]);

        // The first payload must begin after the complete directory, even when the caller supplied only the detection prefix.
        return entry.Reserved is 0
            && entry.BytesInRes is not 0
            && entry.ImageOffset >= IconDir.Size + (dir.Count * IconDirEntry.Size);
    }
}
