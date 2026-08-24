// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Cur;

/// <summary>
/// Describes the CUR image format.
/// </summary>
public sealed class CurFormat : IImageFormat<CurMetadata, CurFrameMetadata>
{
    /// <summary>
    /// Prevents a default instance of the <see cref="CurFormat"/> class from being created.
    /// </summary>
    private CurFormat()
    {
    }

    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static CurFormat Instance { get; } = new();

    /// <inheritdoc/>
    public string Name => "CUR";

    /// <inheritdoc/>
    public string DefaultMimeType => "image/vnd.microsoft.icon";

    /// <inheritdoc/>
    public IEnumerable<string> MimeTypes => CurConstants.MimeTypes;

    /// <inheritdoc/>
    public IEnumerable<string> FileExtensions => CurConstants.FileExtensions;

    /// <inheritdoc/>
    public CurMetadata CreateDefaultFormatMetadata() => new();

    /// <inheritdoc/>
    public CurFrameMetadata CreateDefaultFormatFrameMetadata() => new();
}
