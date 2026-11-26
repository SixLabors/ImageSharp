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
    public int HeaderSize => IconDir.MinHeaderSize;

    /// <inheritdoc/>
    public bool TryDetectFormat(ReadOnlySpan<byte> header, [NotNullWhen(true)] out IImageFormat? format)
    {
        format = IconDir.TryParseFirstEntry(header, IconFileType.CUR, out _) ? CurFormat.Instance : null;
        return format is not null;
    }
}
