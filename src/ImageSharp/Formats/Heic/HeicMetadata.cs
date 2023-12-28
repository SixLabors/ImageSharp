// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heic;

/// <summary>
/// Provides HEIC specific metadata information for the image.
/// </summary>
public class HeicMetadata : IDeepCloneable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HeicMetadata"/> class.
    /// </summary>
    public HeicMetadata()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HeicMetadata"/> class.
    /// </summary>
    /// <param name="other">The metadata to create an instance from.</param>
    private HeicMetadata(HeicMetadata other)
        => this.CompressionMethod = other.CompressionMethod;

    /// <summary>
    /// Gets or sets the compression method used for the primary frame.
    /// </summary>
    public HeicCompressionMethod CompressionMethod { get; set; }

    /// <inheritdoc/>
    public IDeepCloneable DeepClone() => new HeicMetadata(this);
}
