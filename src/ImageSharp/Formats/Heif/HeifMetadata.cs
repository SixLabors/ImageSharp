// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif;

/// <summary>
/// Provides HEIF specific metadata information for the image.
/// </summary>
public class HeifMetadata : IDeepCloneable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HeifMetadata"/> class.
    /// </summary>
    public HeifMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HeifMetadata"/> class.
    /// </summary>
    /// <param name="other">The metadata to create an instance from.</param>
    private HeifMetadata(HeifMetadata other)
        => this.CompressionMethod = other.CompressionMethod;

    /// <summary>
    /// Gets or sets the compression method used for the primary frame.
    /// </summary>
    public HeifCompressionMethod CompressionMethod { get; set; }

    /// <inheritdoc/>
    public IDeepCloneable DeepClone() => new HeifMetadata(this);
}
