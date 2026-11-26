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
    /// <param name="bytesPerRow">The bytes per row.</param>
    /// <param name="width">The number of pixels of a row.</param>
    protected ExrBaseCompression(MemoryAllocator allocator, uint bytesPerRow, int width)
    {
        this.Allocator = allocator;
        this.BytesPerRow = bytesPerRow;
        this.Width = width;
    }

    /// <summary>
    /// Gets the memory allocator.
    /// </summary>
    protected MemoryAllocator Allocator { get; }

    /// <summary>
    /// Gets the bytes per row.
    /// </summary>
    public uint BytesPerRow { get; }

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
