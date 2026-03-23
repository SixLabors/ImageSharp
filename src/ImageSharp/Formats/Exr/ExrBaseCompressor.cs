// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Exr.Constants;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Exr.Compression;

internal abstract class ExrBaseCompressor : ExrBaseCompression
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExrBaseCompressor"/> class.
    /// </summary>
    /// <param name="output">The output stream to write the compressed image to.</param>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="bytesPerBlock">Bytes per row block.</param>
    /// <param name="bytesPerRow">Bytes per pixel row.</param>
    protected ExrBaseCompressor(Stream output, MemoryAllocator allocator, uint bytesPerBlock, uint bytesPerRow)
        : base(allocator, bytesPerBlock, bytesPerRow)
        => this.Output = output;

    /// <summary>
    /// Gets the compression method to use.
    /// </summary>
    public abstract ExrCompression Method { get; }

    /// <summary>
    /// Gets the output stream to write the compressed image to.
    /// </summary>
    public Stream Output { get; }

    /// <summary>
    /// Compresses a block of rows of the image.
    /// </summary>
    /// <param name="rows">Image rows to compress.</param>
    /// <param name="rowCount">The number of rows to compress.</param>
    /// <returns>Number of bytes of of the compressed data.</returns>
    public abstract uint CompressRowBlock(Span<byte> rows, int rowCount);
}
