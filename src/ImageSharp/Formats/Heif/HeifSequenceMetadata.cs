// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Contains the bounded Exif and XMP item payloads implicitly associated with a HEIF image-sequence track.
/// </summary>
internal sealed class HeifSequenceMetadata
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HeifSequenceMetadata"/> class.
    /// </summary>
    /// <param name="exifData">The complete HEIF Exif item payload, or <see langword="null"/>.</param>
    /// <param name="xmpData">The complete XMP packet, or <see langword="null"/>.</param>
    public HeifSequenceMetadata(byte[]? exifData, byte[]? xmpData)
    {
        this.ExifData = exifData;
        this.XmpData = xmpData;
    }

    /// <summary>
    /// Gets the complete HEIF Exif item payload, including its TIFF-header offset field.
    /// </summary>
    public byte[]? ExifData { get; }

    /// <summary>
    /// Gets the raw UTF-8 XMP packet.
    /// </summary>
    public byte[]? XmpData { get; }
}
