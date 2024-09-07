// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.
#nullable disable

using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Webp.Lossless;

/// <summary>
/// Holds information for decoding a lossless webp image.
/// </summary>
internal class Vp8LDecoder : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Vp8LDecoder"/> class.
    /// </summary>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    /// <param name="memoryAllocator">Used for allocating memory for the pixel data output.</param>
    public Vp8LDecoder(int width, int height, MemoryAllocator memoryAllocator)
    {
        this.Width = width;
        this.Height = height;
        this.Metadata = new();
        this.Pixels = memoryAllocator.Allocate<uint>(width * height, AllocationOptions.Clean);
    }

    /// <summary>
    /// Gets or sets the width of the image to decode.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets the height of the image to decode.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Gets or sets the necessary VP8L metadata (like huffman tables) to decode the image.
    /// </summary>
    public Vp8LMetadata Metadata { get; set; }

    /// <summary>
    /// Gets or sets the transformations which needs to be reversed.
    /// </summary>
    public List<Vp8LTransform> Transforms { get; set; }

    /// <summary>
    /// Gets the pixel data.
    /// </summary>
    public IMemoryOwner<uint> Pixels { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Pixels.Dispose();
        this.Metadata?.HuffmanImage?.Dispose();

        if (this.Transforms != null)
        {
            foreach (Vp8LTransform transform in this.Transforms)
            {
                transform.Data?.Dispose();
            }
        }
    }
}
