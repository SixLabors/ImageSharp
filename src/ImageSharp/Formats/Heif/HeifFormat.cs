// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Registers the image encoders, decoders and mime type detectors for the HEIF format.
/// </summary>
public sealed class HeifFormat : IImageFormat<HeifMetadata>
{
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
}
