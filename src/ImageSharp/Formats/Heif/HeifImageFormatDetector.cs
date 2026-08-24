// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Detects HEIF file headers.
/// </summary>
public sealed class HeifImageFormatDetector : IImageFormatDetector
{
    /// <inheritdoc/>
    public int HeaderSize => 32;

    /// <inheritdoc/>
    public bool TryDetectFormat(ReadOnlySpan<byte> header, [NotNullWhen(true)] out IImageFormat? format)
    {
        format = IsSupportedFileFormat(header) ? HeifFormat.Instance : null;
        return format != null;
    }

    private static bool IsSupportedFileFormat(ReadOnlySpan<byte> header)
    {
        if (header.Length < 16 || BinaryPrimitives.ReadUInt32BigEndian(header[4..]) != (uint)Heif4CharCode.Ftyp)
        {
            return false;
        }

        uint boxSize = BinaryPrimitives.ReadUInt32BigEndian(header);
        if (boxSize < 16 || ((boxSize - 16) & 3) != 0)
        {
            return false;
        }

        int availableContentLength = (int)Math.Min(boxSize - 8, (uint)header.Length - 8);
        availableContentLength &= ~3;
        return HeifConstants.IsSupportedFileType(header.Slice(8, availableContentLength));
    }
}
