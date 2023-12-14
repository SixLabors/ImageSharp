// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Icon.Cur;

/// <summary>
/// Registers the image encoders, decoders and mime type detectors for the ICO format.
/// </summary>
public sealed class CurFormat : IImageFormat<CurMetadata, CurFrameMetadata>
{
    private CurFormat()
    {
    }

    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static CurFormat Instance { get; } = new();

    /// <inheritdoc/>
    public string Name => "ICO";

    /// <inheritdoc/>
    public string DefaultMimeType => CurConstants.MimeTypes.First();

    /// <inheritdoc/>
    public IEnumerable<string> MimeTypes => CurConstants.MimeTypes;

    /// <inheritdoc/>
    public IEnumerable<string> FileExtensions => CurConstants.FileExtensions;

    /// <inheritdoc/>
    public CurMetadata CreateDefaultFormatMetadata() => new();

    /// <inheritdoc/>
    public CurFrameMetadata CreateDefaultFormatFrameMetadata() => new();
}
