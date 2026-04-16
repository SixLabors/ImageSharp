// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Exr.Compression;

/// <summary>
/// Base class for EXR compression.
/// </summary>
internal abstract class ExrBaseCompression : IDisposable
{
    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExrBaseCompression" /> class.
    /// </summary>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="bytesPerBlock">The bytes per block.</param>
    /// <param name="bytesPerRow">The bytes per row.</param>
    protected ExrBaseCompression(MemoryAllocator allocator, uint bytesPerBlock, uint bytesPerRow)
    {
        this.Allocator = allocator;
        this.BytesPerBlock = bytesPerBlock;
        this.BytesPerRow = bytesPerRow;
    }

    /// <summary>
    /// Gets the memory allocator.
    /// </summary>
    protected MemoryAllocator Allocator { get; }

    /// <summary>
    /// Gets the bits per pixel.
    /// </summary>
    public int BitsPerPixel { get; }

    /// <summary>
    /// Gets the bytes per row.
    /// </summary>
    public uint BytesPerRow { get; }

    /// <summary>
    /// Gets the uncompressed bytes per block.
    /// </summary>
    public uint BytesPerBlock { get; }

    /// <summary>
    /// Gets the image width.
    /// </summary>
    public int Width { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        this.isDisposed = true;
        this.Dispose(true);
    }

    protected abstract void Dispose(bool disposing);
}
