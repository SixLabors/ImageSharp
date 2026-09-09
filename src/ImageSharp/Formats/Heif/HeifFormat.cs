// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Represents the HEIF image format.
/// </summary>
public sealed class HeifFormat : IImageFormat<HeifMetadata, HeifFrameMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HeifFormat"/> class.
    /// </summary>
    private HeifFormat()
    {
    }

    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static HeifFormat Instance { get; } = new();

    /// <inheritdoc/>
    public string Name => "HEIF";

    /// <inheritdoc/>
    public string DefaultMimeType => "image/heif";

    /// <inheritdoc/>
    public IEnumerable<string> MimeTypes => HeifConstants.MimeTypes;

    /// <inheritdoc/>
    public IEnumerable<string> FileExtensions => HeifConstants.FileExtensions;

    /// <inheritdoc/>
    public HeifMetadata CreateDefaultFormatMetadata() => new();

    /// <inheritdoc/>
    public HeifFrameMetadata CreateDefaultFormatFrameMetadata() => new();
}
