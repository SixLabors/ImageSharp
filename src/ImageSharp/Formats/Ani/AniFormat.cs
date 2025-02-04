// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.


using SixLabors.ImageSharp.Formats.Ico;

namespace SixLabors.ImageSharp.Formats.Ani;


/// <summary>
/// Registers the image encoders, decoders and mime type detectors for the bmp format.
/// </summary>
public sealed class AniFormat : IImageFormat<AniMetadata>
{
    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static AniFormat Instance { get; } = new();

    /// <inheritdoc/>
    public AniMetadata CreateDefaultFormatMetadata() => throw new NotImplementedException();

    /// <inheritdoc/>
    public string Name => "ANI";

    /// <inheritdoc/>
    public string DefaultMimeType { get; }

    /// <inheritdoc/>
    public IEnumerable<string> MimeTypes { get; }

    /// <inheritdoc/>
    public IEnumerable<string> FileExtensions { get; }
}
