// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Exr.Compression.Decompressors;

/// <summary>
/// Implementation of zhe Zip decompressor for EXR image data.
/// </summary>
internal class ZipExrCompression : ExrBaseDecompressor
{
    private readonly IMemoryOwner<byte> tmpBuffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZipExrCompression" /> class.
    /// </summary>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="bytesPerBlock">The bytes per pixel row block.</param>
    /// <param name="bytesPerRow">The bytes per pixel row.</param>
    /// <param name="rowsPerBlock">The pixel rows per block.</param>
    /// <param name="width">The witdh of one row in pixels.</param>
    public ZipExrCompression(MemoryAllocator allocator, uint bytesPerBlock, uint bytesPerRow, uint rowsPerBlock, int width)
        : base(allocator, bytesPerBlock, bytesPerRow, rowsPerBlock, width) => this.tmpBuffer = allocator.Allocate<byte>((int)bytesPerBlock);

    /// <inheritdoc/>
    public override void Decompress(BufferedReadStream stream, uint compressedBytes, Span<byte> buffer)
    {
        Span<byte> uncompressed = this.tmpBuffer.GetSpan();

        uint uncompressedBytes = (uint)buffer.Length;
        int totalRead = UndoZipCompression(stream, compressedBytes, uncompressed, uncompressedBytes);

        Reconstruct(uncompressed, (uint)totalRead);
        Interleave(uncompressed, (uint)totalRead, buffer);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing) => this.tmpBuffer.Dispose();
}
