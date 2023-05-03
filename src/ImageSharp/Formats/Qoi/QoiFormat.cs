// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Png;

namespace SixLabors.ImageSharp.Formats.Qoi;

/// <summary>
/// Registers the image encoders, decoders and mime type detectors for the qoi format.
/// </summary>
public sealed class QoiFormat : IImageFormat<QoiMetadata>
{
    private QoiFormat()
    { }

    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static QoiFormat Instance { get; } = new QoiFormat();

    /// <inheritdoc/>
    public QoiMetadata CreateDefaultFormatMetadata() => new();

    /// <inheritdoc/>
    public string Name => "QOI";

    /// <inheritdoc/>
    public string DefaultMimeType => "image/qoi";

    /// <inheritdoc/>
    public IEnumerable<string> MimeTypes => QoiConstants.MimeTypes;

    /// <inheritdoc/>
    public IEnumerable<string> FileExtensions => QoiConstants.FileExtensions;
}
