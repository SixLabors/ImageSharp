// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Represents the JPEG XL image format.
/// </summary>
public sealed class JxlFormat : IImageFormat<JxlMetadata, JxlFrameMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JxlFormat"/> class.
    /// </summary>
    private JxlFormat()
    {
    }

    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static JxlFormat Instance { get; } = new();

    /// <inheritdoc/>
    public string Name => "JPEG XL";

    /// <inheritdoc/>
    public string DefaultMimeType => "image/jxl";

    /// <inheritdoc/>
    public IEnumerable<string> MimeTypes => JxlConstants.MimeTypes;

    /// <inheritdoc/>
    public IEnumerable<string> FileExtensions => JxlConstants.FileExtensions;

    /// <inheritdoc/>
    public JxlMetadata CreateDefaultFormatMetadata() => new();

    /// <inheritdoc/>
    public JxlFrameMetadata CreateDefaultFormatFrameMetadata() => new();
}
