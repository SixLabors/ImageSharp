// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Checks if the first few bytes of a file represent
/// JPEG XL.
/// </summary>
public sealed class JxlImageFormatDetector : IImageFormatDetector
{
    /// <summary>
    /// Gets file signature bytes which represent a container-based
    /// JPEG XL file.
    /// </summary>
    private static ReadOnlySpan<byte> ContainerStart =>
    [
        0x00, 0x00, 0x00, 0x0C,
        0x4A, 0x58, 0x4C, 0x20,
        0x0D, 0x0A, 0x87, 0x0A,
    ];

    /// <inheritdoc/>
    public int HeaderSize => 12;

    /// <inheritdoc/>
    public bool TryDetectFormat(ReadOnlySpan<byte> header, [NotNullWhen(true)] out IImageFormat? format)
    {
        if (header.StartsWith([(byte)0xFF, (byte)0x0A]))
        {
            // Just codestream.
            format = new JxlFormat();
            return true;
        }
        else if (header.SequenceEqual(ContainerStart))
        {
            // Container format.
            format = new JxlFormat();
            return true;
        }
        else
        {
            format = null;
            return false;
        }
    }
}
