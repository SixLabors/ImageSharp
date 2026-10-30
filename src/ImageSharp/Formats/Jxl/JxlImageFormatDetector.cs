// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Detects raw JPEG XL codestreams and JPEG XL containers.
/// </summary>
public sealed class JxlImageFormatDetector : IImageFormatDetector
{
    /// <inheritdoc/>
    public int HeaderSize => 12;

    /// <inheritdoc/>
    public bool TryDetectFormat(ReadOnlySpan<byte> header, [NotNullWhen(true)] out IImageFormat? format)
    {
        // The raw signature needs only two bytes. Container detection needs the entire signature box.
        // StartsWith also accepts the larger header spans supplied by a manager that has other detectors.
        bool matches = header.StartsWith(JxlConstants.CodestreamSignature) || header.StartsWith(JxlConstants.ContainerSignature);
        format = matches ? JxlFormat.Instance : null;
        return matches;
    }
}
