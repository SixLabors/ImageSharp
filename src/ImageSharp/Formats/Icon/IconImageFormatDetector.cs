// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Icon;

/// <summary>
/// Detects ico file headers.
/// </summary>
public class IconImageFormatDetector : IImageFormatDetector
{
    /// <inheritdoc/>
    public int HeaderSize { get; } = 4;

    /// <inheritdoc/>
    public bool TryDetectFormat(ReadOnlySpan<byte> header, [NotNullWhen(true)] out IImageFormat? format)
    {
        switch (MemoryMarshal.Cast<byte, uint>(header)[0])
        {
            case Ico.IcoConstants.FileHeader:
                format = Ico.IcoFormat.Instance;
                return true;
            case Cur.CurConstants.FileHeader:
                format = Cur.CurFormat.Instance;
                return true;
            default:
                format = default;
                return false;
        }
    }
}
