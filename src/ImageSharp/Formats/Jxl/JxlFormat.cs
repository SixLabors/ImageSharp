// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// JPEG XL format
/// </summary>
public sealed class JxlFormat : IImageFormat
{
    /// <inheritdoc />
    public string Name => "JPEG XL";

    /// <inheritdoc />
    public string DefaultMimeType => "image/jxl";

    /// <inheritdoc />
    IEnumerable<string> IImageFormat.MimeTypes => new[] { "image/jxl" };

    /// <inheritdoc />
    IEnumerable<string> IImageFormat.FileExtensions => new[] { "jxl" };
}
