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
    /// <param name="bytesPerBlock">Bytes per block.</param>
    protected ExrBaseCompressor(Stream output, MemoryAllocator allocator, uint bytesPerBlock)
        : base(allocator, bytesPerBlock)
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
    /// Does any initialization required for the compression.
    /// </summary>
    /// <param name="rowsPerBlock">The number of rows per block.</param>
    public abstract void Initialize(int rowsPerBlock);

    /// <summary>
    /// Compresses a strip of the image.
    /// </summary>
    /// <param name="rows">Image rows to compress.</param>
    /// <param name="height">Image height.</param>
    public abstract void CompressStrip(Span<byte> rows, int height);
}
