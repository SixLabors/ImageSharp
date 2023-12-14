// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Icon.Ico;

/// <summary>
/// Registers the image encoders, decoders and mime type detectors for the ICO format.
/// </summary>
public sealed class IcoFormat : IImageFormat<IcoMetadata, IcoFrameMetadata>
{
    private IcoFormat()
    {
    }

    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static IcoFormat Instance { get; } = new();

    /// <inheritdoc/>
    public string Name => "ICO";

    /// <inheritdoc/>
    public string DefaultMimeType => IcoConstants.MimeTypes.First();

    /// <inheritdoc/>
    public IEnumerable<string> MimeTypes => IcoConstants.MimeTypes;

    /// <inheritdoc/>
    public IEnumerable<string> FileExtensions => IcoConstants.FileExtensions;

    /// <inheritdoc/>
    public IcoMetadata CreateDefaultFormatMetadata() => new();

    /// <inheritdoc/>
    public IcoFrameMetadata CreateDefaultFormatFrameMetadata() => new();
}
