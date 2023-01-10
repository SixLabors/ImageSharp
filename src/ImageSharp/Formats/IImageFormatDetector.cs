// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Diagnostics.CodeAnalysis;

namespace SixLabors.ImageSharp.Formats;

/// <summary>
/// Used for detecting mime types from a file header
/// </summary>
public interface IImageFormatDetector
{
    /// <summary>
    /// Gets the size of the header for this image type.
    /// </summary>
    /// <value>The size of the header.</value>
    int HeaderSize { get; }

    /// <summary>
    /// Detect mimetype
    /// </summary>
    /// <param name="header">The <see cref="T:byte[]"/> containing the file header.</param>
    /// <param name="format">The mime type of detected otherwise returns null</param>
    /// <returns>returns true when format was detected otherwise false.</returns>
    bool TryDetectFormat(ReadOnlySpan<byte> header, [NotNullWhen(true)] out IImageFormat? format);
}
