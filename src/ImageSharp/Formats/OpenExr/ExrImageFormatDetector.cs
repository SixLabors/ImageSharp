// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;

namespace SixLabors.ImageSharp.Formats.OpenExr;

/// <summary>
/// Detects OpenExr file headers.
/// </summary>
public sealed class ExrImageFormatDetector : IImageFormatDetector
{
    /// <inheritdoc/>
    public int HeaderSize => 4;

    /// <inheritdoc/>
    public IImageFormat DetectFormat(ReadOnlySpan<byte> header) => this.IsSupportedFileFormat(header) ? ExrFormat.Instance : null;

    private bool IsSupportedFileFormat(ReadOnlySpan<byte> header)
    {
        if (header.Length >= this.HeaderSize)
        {
            int fileTypeMarker = BinaryPrimitives.ReadInt32LittleEndian(header);
            return fileTypeMarker == ExrConstants.MagickBytes;
        }

        return false;
    }
}
