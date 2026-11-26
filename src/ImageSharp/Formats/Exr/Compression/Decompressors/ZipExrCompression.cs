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
    /// <param name="width">The witdh of one row in pixels.</param>
    /// <param name="integrityHandling">The segment integrity mode of the decode.</param>
    public ZipExrCompression(MemoryAllocator allocator, uint bytesPerBlock, uint bytesPerRow, int width, SegmentIntegrityHandling integrityHandling)
        : base(allocator, bytesPerRow, width, integrityHandling) => this.tmpBuffer = allocator.Allocate<byte>((int)bytesPerBlock);

    /// <inheritdoc/>
    protected override bool DecompressCore(BufferedReadStream stream, uint compressedBytes, uint uncompressedBytes, Span<byte> buffer)
    {
        Span<byte> uncompressed = this.tmpBuffer.GetSpan()[..(int)uncompressedBytes];

        if (!this.UndoZipCompression(stream, compressedBytes, uncompressed, uncompressedBytes))
        {
            return false;
        }

        Reconstruct(uncompressed, uncompressedBytes);
        Interleave(uncompressed, uncompressedBytes, buffer);
        return true;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        this.tmpBuffer.Dispose();
        base.Dispose(disposing);
    }
}
