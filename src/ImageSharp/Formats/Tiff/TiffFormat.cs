// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Tiff.Constants;

namespace SixLabors.ImageSharp.Formats.Tiff;

/// <summary>
/// Encapsulates the means to encode and decode Tiff images.
/// </summary>
public sealed class TiffFormat : IImageFormat<TiffMetadata, TiffFrameMetadata>
{
    private TiffFormat()
    {
    }

    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static TiffFormat Instance { get; } = new();

    /// <inheritdoc/>
    public string Name => "TIFF";

    /// <inheritdoc/>
    public string DefaultMimeType => "image/tiff";

    /// <inheritdoc/>
    public IEnumerable<string> MimeTypes => TiffConstants.MimeTypes;

    /// <inheritdoc/>
    public IEnumerable<string> FileExtensions => TiffConstants.FileExtensions;

    /// <inheritdoc/>
    public TiffMetadata CreateDefaultFormatMetadata() => new();

    /// <inheritdoc/>
    public TiffFrameMetadata CreateDefaultFormatFrameMetadata() => new();
}
