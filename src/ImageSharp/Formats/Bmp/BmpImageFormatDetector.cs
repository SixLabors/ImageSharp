// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace SixLabors.ImageSharp.Formats.Bmp;

/// <summary>
/// Detects bmp file headers.
/// </summary>
public sealed class BmpImageFormatDetector : IImageFormatDetector
{
    /// <inheritdoc/>
    public int HeaderSize => 2;

    /// <inheritdoc/>
    public bool TryDetectFormat(ReadOnlySpan<byte> header, [NotNullWhen(true)] out IImageFormat? format)
    {
        format = this.IsSupportedFileFormat(header) ? BmpFormat.Instance : null;

        return format != null;
    }

    private bool IsSupportedFileFormat(ReadOnlySpan<byte> header)
    {
        if (header.Length >= this.HeaderSize)
        {
            short fileTypeMarker = BinaryPrimitives.ReadInt16LittleEndian(header);
            return fileTypeMarker == BmpConstants.TypeMarkers.Bitmap ||
                   fileTypeMarker == BmpConstants.TypeMarkers.BitmapArray;
        }

        return false;
    }
}
