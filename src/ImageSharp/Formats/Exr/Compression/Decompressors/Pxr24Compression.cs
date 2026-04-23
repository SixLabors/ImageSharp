// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.IO.Compression;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Compression.Zlib;
using SixLabors.ImageSharp.IO;
using SixLabors.ImageSharp.Memory;

namespace SixLabors.ImageSharp.Formats.Exr.Compression.Decompressors;

internal class Pxr24Compression : ExrBaseDecompressor
{
    private readonly IMemoryOwner<byte> tmpBuffer;

    private readonly uint rowsPerBlock;

    private readonly int channelCount;

    private readonly int width;

    /// <summary>
    /// Initializes a new instance of the <see cref="Pxr24Compression" /> class.
    /// </summary>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="bytesPerBlock">The bytes per pixel row block.</param>
    /// <param name="bytesPerRow">The bytes per pixel row.</param>
    public Pxr24Compression(MemoryAllocator allocator, uint bytesPerBlock, uint bytesPerRow, uint rowsPerBlock, int width, int channelCount)
        : base(allocator, bytesPerBlock, bytesPerRow)
    {
        this.tmpBuffer = allocator.Allocate<byte>((int)bytesPerBlock);
        this.rowsPerBlock = rowsPerBlock;
        this.width = width;
        this.channelCount = channelCount;
    }

    /// <inheritdoc/>
    public override void Decompress(BufferedReadStream stream, uint compressedBytes, Span<byte> buffer)
    {
        Span<byte> uncompressed = this.tmpBuffer.GetSpan();
        Span<ushort> outputBuffer = MemoryMarshal.Cast<byte, ushort>(buffer);

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

        int lastIn = 0;
        int outputOffset = 0;
        for (int y = 0; y < this.rowsPerBlock; y++)
        {
            for (int c = 0; c < this.channelCount; c++)
            {
                int offsetT1 = lastIn;
                lastIn += this.width;
                int offsetT2 = lastIn;
                lastIn += this.width;
                uint pixel = 0;
                for (int x = 0; x < this.width; x++)
                {
                    uint t1 = uncompressed[offsetT1];
                    uint t2 = uncompressed[offsetT2];
                    uint diff = (t1 << 8) | t2;

                    pixel += diff;
                    outputBuffer[outputOffset] = (ushort)pixel;

                    offsetT1++;
                    offsetT2++;
                    outputOffset++;
                }
            }
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing) => this.tmpBuffer.Dispose();
}
