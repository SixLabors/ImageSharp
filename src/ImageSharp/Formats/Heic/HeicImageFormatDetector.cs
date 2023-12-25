// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;

namespace SixLabors.ImageSharp.Formats.Heic;

/// <summary>
/// Detects HEIC file headers.
/// </summary>
public sealed class HeicImageFormatDetector : IImageFormatDetector
{
    /// <inheritdoc/>
    public bool TryDetectFormat(ReadOnlySpan<byte> header, [NotNullWhen(true)] out IImageFormat? format)
    {
        format = IsSupportedFileFormat(header) ? HeicFormat.Instance : null;
        return format != null;
    }

    private static bool IsSupportedFileFormat(ReadOnlySpan<byte> header)
    {
        // TODO: Implement

        return false;
    }
}
