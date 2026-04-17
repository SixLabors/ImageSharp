// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.IO.Compression;
using SixLabors.ImageSharp.Compression.Zlib;
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
    public ZipExrCompression(MemoryAllocator allocator, uint bytesPerBlock, uint bytesPerRow)
        : base(allocator, bytesPerBlock, bytesPerRow) => this.tmpBuffer = allocator.Allocate<byte>((int)bytesPerBlock);

    /// <inheritdoc/>
    public override void Decompress(BufferedReadStream stream, uint compressedBytes, Span<byte> buffer)
    {
        Span<byte> uncompressed = this.tmpBuffer.GetSpan();

        long pos = stream.Position;
        using ZlibInflateStream inflateStream = new(
                   stream,
                   () =>
                   {
                       int left = (int)(compressedBytes - (stream.Position - pos));
                       return left > 0 ? left : 0;
                   });
        inflateStream.AllocateNewBytes((int)this.BytesPerBlock, true);
        using DeflateStream dataStream = inflateStream.CompressedStream!;

        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int bytesRead = dataStream.Read(uncompressed, totalRead, buffer.Length - totalRead);
            if (bytesRead <= 0)
            {
                break;
            }

            totalRead += bytesRead;
        }

        if (totalRead == 0)
        {
            ExrThrowHelper.ThrowInvalidImageContentException("Could not read enough data for zip compressed image data!");
        }

        Reconstruct(uncompressed, (uint)totalRead);
        Interleave(uncompressed, (uint)totalRead, buffer);
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing) => this.tmpBuffer.Dispose();
}
