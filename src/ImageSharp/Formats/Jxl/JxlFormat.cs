// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Registers the JPEG XL format.
/// </summary>
public sealed class JxlFormat : IImageFormat<JxlMetadata>
{
    private JxlFormat()
    {
    }

    /// <summary>Gets the shared instance.</summary>
    public static JxlFormat Instance { get; } = new();

    /// <inheritdoc/>
    public string DefaultMimeType => "image/jxl";

    /// <inheritdoc/>
    public string Name => "JXL";

    /// <inheritdoc/>
    public IEnumerable<string> MimeTypes => JxlConstants.MimeTypes;

    /// <inheritdoc/>
    public IEnumerable<string> FileExtensions => JxlConstants.FileExtensions;

    /// <inheritdoc/>
    public JxlMetadata CreateDefaultFormatMetadata() => new();
}
