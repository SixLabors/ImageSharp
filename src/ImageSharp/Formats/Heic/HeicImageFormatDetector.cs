// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;

namespace SixLabors.ImageSharp.Formats.Heic;

/// <summary>
/// Detects HEIC file headers.
/// </summary>
public sealed class HeicImageFormatDetector : IImageFormatDetector
{
    /// <inheritdoc/>
    public int HeaderSize => 12;

    /// <inheritdoc/>
    public bool TryDetectFormat(ReadOnlySpan<byte> header, [NotNullWhen(true)] out IImageFormat? format)
    {
        format = IsSupportedFileFormat(header) ? HeicFormat.Instance : null;
        return format != null;
    }

    private static bool IsSupportedFileFormat(ReadOnlySpan<byte> header) =>
        BinaryPrimitives.ReadUInt32BigEndian(header.Slice(4)) == (uint)Heic4CharCode.ftyp &&
        BinaryPrimitives.ReadUInt32BigEndian(header.Slice(8)) == (uint)Heic4CharCode.heic;
}
