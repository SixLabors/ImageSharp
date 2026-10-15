// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Detects JPEG XL file headers: the codestream signature (FF 0A) or the
/// 12-byte container signature.
/// </summary>
public class JxlImageFormatDetector : IImageFormatDetector
{
    /// <inheritdoc/>
    public int HeaderSize => 12;

    /// <inheritdoc/>
    public bool TryDetectFormat(ReadOnlySpan<byte> header, [NotNullWhen(true)] out IImageFormat? format)
    {
        format = this.IsSupportedFileFormat(header) ? JxlFormat.Instance : null;
        return format != null;
    }

    private bool IsSupportedFileFormat(ReadOnlySpan<byte> header)
        => header.Length >= 2
           && (JxlConstants.CodestreamMagic.SequenceEqual(header[..2])
               || (header.Length >= this.HeaderSize
                   && JxlConstants.ContainerMagic.SequenceEqual(header[..12])));
}
