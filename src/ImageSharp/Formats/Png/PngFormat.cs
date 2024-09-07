// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Png;

/// <summary>
/// Registers the image encoders, decoders and mime type detectors for the png format.
/// </summary>
public sealed class PngFormat : IImageFormat<PngMetadata, PngFrameMetadata>
{
    private PngFormat()
    {
    }

    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static PngFormat Instance { get; } = new();

    /// <inheritdoc/>
    public string Name => "PNG";

    /// <inheritdoc/>
    public string DefaultMimeType => "image/png";

    /// <inheritdoc/>
    public IEnumerable<string> MimeTypes => PngConstants.MimeTypes;

    /// <inheritdoc/>
    public IEnumerable<string> FileExtensions => PngConstants.FileExtensions;

    /// <inheritdoc/>
    public PngMetadata CreateDefaultFormatMetadata() => new();

    /// <inheritdoc/>
    public PngFrameMetadata CreateDefaultFormatFrameMetadata() => new();
}
