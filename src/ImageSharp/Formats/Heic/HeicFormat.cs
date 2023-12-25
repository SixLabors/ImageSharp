// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heic;

/// <summary>
/// Registers the image encoders, decoders and mime type detectors for the HEIC format.
/// </summary>
public sealed class HeicFormat : IImageFormat<HeicMetadata>
{
    private HeicFormat()
    {
    }

    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static HeicFormat Instance { get; } = new();

    /// <inheritdoc/>
    public string Name => "HEIC";

    /// <inheritdoc/>
    public string DefaultMimeType => "image/heif";

    /// <inheritdoc/>
    public IEnumerable<string> MimeTypes => HeicConstants.MimeTypes;

    /// <inheritdoc/>
    public IEnumerable<string> FileExtensions => HeicConstants.FileExtensions;

    /// <inheritdoc/>
    public HeicMetadata CreateDefaultFormatMetadata() => new();
}
