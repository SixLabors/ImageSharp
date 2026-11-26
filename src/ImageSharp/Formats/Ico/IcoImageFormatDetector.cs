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
    public int HeaderSize => IconDir.MinHeaderSize;

    /// <inheritdoc/>
    public bool TryDetectFormat(ReadOnlySpan<byte> header, [NotNullWhen(true)] out IImageFormat? format)
    {
        format = IsSupportedFileFormat(header) ? IcoFormat.Instance : null;
        return format is not null;
    }

    /// <summary>
    /// Determines whether the supplied header contains a valid ICO directory and first entry.
    /// </summary>
    /// <param name="header">The candidate file header.</param>
    /// <returns><see langword="true"/> when the header identifies ICO data.</returns>
    private static bool IsSupportedFileFormat(ReadOnlySpan<byte> header)
        => IconDir.TryParseFirstEntry(header, IconFileType.ICO, out IconDirEntry entry)
            && entry.Planes is 0 or 1
            && entry.BitCount is 1 or 4 or 8 or 16 or 24 or 32;
}
