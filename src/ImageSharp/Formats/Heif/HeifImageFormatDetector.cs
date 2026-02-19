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
    public int HeaderSize => 12;

    /// <inheritdoc/>
    public bool TryDetectFormat(ReadOnlySpan<byte> header, [NotNullWhen(true)] out IImageFormat? format)
    {
        format = IsSupportedFileFormat(header) ? HeifFormat.Instance : null;
        return format != null;
    }

    private static bool IsSupportedFileFormat(ReadOnlySpan<byte> header)
    {
        bool hasFtyp = BinaryPrimitives.ReadUInt32BigEndian(header.Slice(4)) == (uint)Heif4CharCode.Ftyp;
        uint brand = BinaryPrimitives.ReadUInt32BigEndian(header.Slice(8));
        return hasFtyp && (brand == (uint)Heif4CharCode.Heic || brand == (uint)Heif4CharCode.Heix || brand == (uint)Heif4CharCode.Avif);
    }
}
