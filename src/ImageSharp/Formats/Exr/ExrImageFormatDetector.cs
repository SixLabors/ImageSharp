// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace SixLabors.ImageSharp.Formats.Exr;

/// <summary>
/// Detects OpenExr file headers.
/// </summary>
public sealed class ExrImageFormatDetector : IImageFormatDetector
{
    /// <inheritdoc/>
    public int HeaderSize => 4;

    private bool IsSupportedFileFormat(ReadOnlySpan<byte> header)
    {
        if (header.Length >= this.HeaderSize)
        {
            int fileTypeMarker = BinaryPrimitives.ReadInt32LittleEndian(header);
            return fileTypeMarker == ExrConstants.MagickBytes;
        }

        return false;
    }

    /// <inheritdoc/>
    public bool TryDetectFormat(ReadOnlySpan<byte> header, [NotNullWhen(true)] out IImageFormat? format)
    {
        format = this.IsSupportedFileFormat(header) ? ExrFormat.Instance : null;
        return format != null;
    }
}
