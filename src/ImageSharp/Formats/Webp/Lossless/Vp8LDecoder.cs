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
    // The header parse changes Width to the packed width of a color-indexed image. The pixel data needs the full image size.
    private readonly int pixelCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="Vp8LDecoder"/> class.
    /// </summary>
    /// <param name="width">The width of the image.</param>
    /// <param name="height">The height of the image.</param>
    public Vp8LDecoder(int width, int height)
    {
        this.Width = width;
        this.Height = height;
        this.pixelCount = width * height;
        this.Metadata = new Vp8LMetadata();
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
    public IMemoryOwner<uint> Pixels { get; private set; }

    /// <summary>
    /// Allocates the pixel data for the decode of the image stream.
    /// </summary>
    /// <remarks>
    /// The header parse needs no pixel data, and it runs before the decoder creates the image. The decoder calls this method after it creates
    /// the image. Thus the pixel data comes after the image buffer.
    /// </remarks>
    /// <param name="memoryAllocator">The memory allocator.</param>
    public void AllocatePixels(MemoryAllocator memoryAllocator)
        => this.Pixels = memoryAllocator.Allocate<uint>(this.pixelCount, AllocationOptions.Clean);

    /// <inheritdoc/>
    public void Dispose()
    {
        // A damaged image can stop after the header parse, before AllocatePixels runs. Then the pixel data does not exist.
        this.Pixels?.Dispose();
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
