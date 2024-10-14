// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Qoi;

/// <summary>
/// Registers the image encoders, decoders and mime type detectors for the qoi format.
/// </summary>
public sealed class QoiFormat : IImageFormat<QoiMetadata>
{
    private QoiFormat()
    {
    }

    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static QoiFormat Instance { get; } = new();

    /// <inheritdoc/>
    public string DefaultMimeType => "image/qoi";

    /// <inheritdoc/>
    public string Name => "QOI";

    /// <inheritdoc/>
    public IEnumerable<string> MimeTypes => QoiConstants.MimeTypes;

    /// <inheritdoc/>
    public IEnumerable<string> FileExtensions => QoiConstants.FileExtensions;

    /// <inheritdoc/>
    public QoiMetadata CreateDefaultFormatMetadata() => new();
}
