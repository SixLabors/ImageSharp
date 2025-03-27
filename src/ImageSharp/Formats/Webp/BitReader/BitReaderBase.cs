// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Webp.BitReader;

/// <summary>
/// Base class for VP8 and VP8L bitreader.
/// </summary>
internal abstract class BitReaderBase : IDisposable
{
    private bool isDisposed;

    protected BitReaderBase(IMemoryOwner<byte> data)
        => this.Data = data;

    protected BitReaderBase(Stream inputStream, int imageDataSize, MemoryAllocator memoryAllocator)
        => this.Data = ReadImageDataFromStream(inputStream, imageDataSize, memoryAllocator);

    /// <summary>
    /// Gets the raw encoded image data.
    /// </summary>
    public IMemoryOwner<byte> Data { get; }

    /// <summary>
    /// Copies the raw encoded image data from the stream into a byte array.
    /// </summary>
    /// <param name="input">The input stream.</param>
    /// <param name="bytesToRead">Number of bytes to read as indicated from the chunk size.</param>
    /// <param name="memoryAllocator">Used for allocating memory during reading data from the stream.</param>
    protected static IMemoryOwner<byte> ReadImageDataFromStream(Stream input, int bytesToRead, MemoryAllocator memoryAllocator)
    {
        IMemoryOwner<byte> data = memoryAllocator.Allocate<byte>(bytesToRead, AllocationOptions.Clean);
        Span<byte> dataSpan = data.Memory.Span;
        input.Read(dataSpan[..bytesToRead], 0, bytesToRead);

        return data;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (this.isDisposed)
        {
            return;
        }

        if (disposing)
        {
            this.Data.Dispose();
        }

        this.isDisposed = true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
