// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Webp;

/// <summary>
/// Registers the image encoders, decoders and mime type detectors for the Webp format.
/// </summary>
public sealed class WebpFormat : IImageFormat<WebpMetadata, WebpFrameMetadata>
{
    private WebpFormat()
    {
    }

    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static WebpFormat Instance { get; } = new();

    /// <inheritdoc/>
    public string Name => "WEBP";

    /// <inheritdoc/>
    public string DefaultMimeType => "image/webp";

    /// <inheritdoc/>
    public IEnumerable<string> MimeTypes => WebpConstants.MimeTypes;

    /// <inheritdoc/>
    public IEnumerable<string> FileExtensions => WebpConstants.FileExtensions;

    /// <inheritdoc/>
    public WebpMetadata CreateDefaultFormatMetadata() => new();

    /// <inheritdoc/>
    public WebpFrameMetadata CreateDefaultFormatFrameMetadata() => new();
}
