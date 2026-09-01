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
        return format is not null;
    }

    /// <summary>
    /// Determines whether the available header begins with a supported HEIF image file-type box.
    /// </summary>
    /// <param name="header">The fixed-size header prefix supplied by format detection.</param>
    /// <returns><see langword="true"/> when the prefix declares a supported image or image-sequence brand.</returns>
    private static bool IsSupportedFileFormat(ReadOnlySpan<byte> header)
    {
        // Detection is intentionally limited to files beginning with ftyp. Other valid top-level boxes can precede
        // ftyp in ISO BMFF, but scanning arbitrary input is outside the fixed-header detector contract.
        if (header.Length < 16 || BinaryPrimitives.ReadUInt32BigEndian(header[4..]) != (uint)Heif4CharCode.Ftyp)
        {
            return false;
        }

        uint compactBoxSize = BinaryPrimitives.ReadUInt32BigEndian(header);
        int boxHeaderSize = 8;
        ulong boxSize = compactBoxSize;
        if (compactBoxSize == 1)
        {
            // An extended-size box inserts its 64-bit size before the normal ftyp payload.
            if (header.Length < 24)
            {
                return false;
            }

            boxHeaderSize = 16;
            boxSize = BinaryPrimitives.ReadUInt64BigEndian(header[8..]);
        }

        if (boxSize < (uint)(boxHeaderSize + 8))
        {
            return false;
        }

        ulong boxContentLength = boxSize - (uint)boxHeaderSize;
        if ((boxContentLength & 3) != 0)
        {
            return false;
        }

        // HeaderSize may expose only a prefix of a longer ftyp box. Whole compatible-brand codes in that prefix are
        // sufficient for detection; the decoder validates the complete box before reading the rest of the container.
        int availableContentLength = (int)Math.Min(boxContentLength, (ulong)(header.Length - boxHeaderSize));
        availableContentLength &= ~3;
        return HeifConstants.TryGetFileType(header.Slice(boxHeaderSize, availableContentLength), out _);
    }
}
