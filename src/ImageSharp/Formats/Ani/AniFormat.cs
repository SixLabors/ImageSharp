// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Ani;

/// <summary>
/// Describes the ANI image format.
/// </summary>
public sealed class AniFormat : IImageFormat<AniMetadata, AniFrameMetadata>
{
    /// <summary>
    /// Prevents a default instance of the <see cref="AniFormat"/> class from being created.
    /// </summary>
    private AniFormat()
    {
    }

    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static AniFormat Instance { get; } = new();

    /// <inheritdoc/>
    public string Name => "ANI";

    /// <inheritdoc/>
    public string DefaultMimeType => "application/x-navi-animation";

    /// <inheritdoc/>
    public IEnumerable<string> MimeTypes => AniConstants.MimeTypes;

    /// <inheritdoc/>
    public IEnumerable<string> FileExtensions => AniConstants.FileExtensions;

    /// <inheritdoc/>
    public AniMetadata CreateDefaultFormatMetadata() => new();

    /// <inheritdoc/>
    public AniFrameMetadata CreateDefaultFormatFrameMetadata() => new();
}
