// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Pbm;

/// <summary>
/// Detects Pbm file headers.
/// </summary>
public sealed class PbmImageFormatDetector : IImageFormatDetector
{
    private const byte P = (byte)'P';
    private const byte Zero = (byte)'0';
    private const byte Seven = (byte)'7';

    /// <inheritdoc/>
    public int HeaderSize => 2;

    /// <inheritdoc/>
    public IImageFormat DetectFormat(ReadOnlySpan<byte> header) => IsSupportedFileFormat(header) ? PbmFormat.Instance : null;

    private static bool IsSupportedFileFormat(ReadOnlySpan<byte> header)
    {
        if ((uint)header.Length > 1)
        {
            // Signature should be between P1 and P6.
            return header[0] == P && (uint)(header[1] - Zero - 1) < (Seven - Zero - 1);
        }

        return false;
    }
}
