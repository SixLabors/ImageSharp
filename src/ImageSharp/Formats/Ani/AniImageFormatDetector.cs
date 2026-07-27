// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;

namespace SixLabors.ImageSharp.Formats.Ani;

/// <summary>
/// Detects ANI file headers.
/// </summary>
public sealed class AniImageFormatDetector : IImageFormatDetector
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AniImageFormatDetector"/> class.
    /// </summary>
    public AniImageFormatDetector()
    {
    }

    /// <inheritdoc/>
    public int HeaderSize => AniConstants.RiffHeaderSize;

    /// <inheritdoc/>
    public bool TryDetectFormat(ReadOnlySpan<byte> header, [NotNullWhen(true)] out IImageFormat? format)
    {
        format = this.IsSupportedFileFormat(header) ? AniFormat.Instance : null;
        return format is not null;
    }

    /// <summary>
    /// Determines whether the supplied header is a RIFF container with the ANI "ACON" form type.
    /// </summary>
    /// <param name="header">The candidate file header.</param>
    /// <returns><see langword="true"/> when the header identifies ANI data.</returns>
    private bool IsSupportedFileFormat(ReadOnlySpan<byte> header)
        => header.Length >= this.HeaderSize
        && header[..4].SequenceEqual(AniConstants.RiffFourCc)
        && header.Slice(8, 4).SequenceEqual(AniConstants.AniFormTypeFourCc);
}
